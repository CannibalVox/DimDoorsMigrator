using fNbt;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DimDoorsMigrator
{
    class Program
    {
        static Dictionary<int, string> legacyBlockMap = new Dictionary<int, string>();

        static void Main(string[] args)
        {
            using (FileStream file = new FileStream("blockMapping.txt", FileMode.Open))
            {
                using (StreamReader reader = new StreamReader(file))
                {
                    while (!reader.EndOfStream)
                    {
                        String line = reader.ReadLine();
                        String[] tokens = line.Split(',');
                        int blockId;
                        if (int.TryParse(tokens[0], out blockId))
                        {
                            legacyBlockMap[blockId] = tokens[1];
                        }
                    }
                }
            }

            legacyBlockMap[1973] = "dimdoors:Fabric of Reality";
            legacyBlockMap[220] = "dimdoors:Fabric of RealityPerm";
            legacyBlockMap[1975] = "dimdoors:Warp Door";
            legacyBlockMap[1970] = "dimdoors:Dimensional Door";
            legacyBlockMap[1979] = "dimdoors:transientDoor";

            Directory.CreateDirectory(@".\out-schematics\");
            foreach (string schematic in Directory.GetFiles(@".\schematics\", "*.schematic", SearchOption.AllDirectories))
            {
                String outPath = @".\out-schematics\" + schematic.Substring(schematic.IndexOf(@"\schematics\") + @"\schematics\".Length);
                String path = Path.GetDirectoryName(outPath);
                Directory.CreateDirectory(path);


                NbtFile inFile = new NbtFile(schematic);
                NbtCompound compound = inFile.RootTag;
                byte[] lowBits = compound.Get("Blocks").ByteArrayValue;
                byte[] highBits = compound.Get("AddBlocks").ByteArrayValue;
                Boolean hasExtendedBlocks = highBits.Length != 0;
                short[] allBlocks = new short[lowBits.Length];
                for (int i = 0; i < lowBits.Length; i += 2)
                {
                    allBlocks[i] = lowBits[i];
                    if (i < lowBits.Length - 1)
                    {
                        allBlocks[i + 1] = lowBits[i + 1];
                    }

                    if (hasExtendedBlocks)
                    {
                        byte splitByte = highBits[i >> 1];
                        short block1 = (short)((splitByte & 0x0F) << 8);
                        short block2 = (short)((splitByte & 0xF0) << 4);
                        allBlocks[i] += block1;
                        if (i < lowBits.Length - 1)
                        {
                            allBlocks[i + 1] += block2;
                        }
                    }
                }

                List<string> palette = new List<string>();
                for (int i = 0; i < allBlocks.Length; i++)
                {
                    string blockName = legacyBlockMap[allBlocks[i]];
                    if (!palette.Contains(blockName))
                        palette.Add(blockName);
                    allBlocks[i] = (short)palette.IndexOf(blockName);
                }

                for (int i = 0; i < allBlocks.Length; i += 2)
                {
                    lowBits[i] = (byte)(allBlocks[i] & 0xFF);
                    if (i < allBlocks.Length - 1)
                    {
                        lowBits[i + 1] = (byte)(allBlocks[i + 1] & 0xFF);
                    }

                    byte block1 = (byte)((allBlocks[i] >> 8) & 0x0F);
                    byte block2 = 0;
                    if (i < allBlocks.Length - 1)
                    {
                        block2 = (byte)((allBlocks[i + 1] >> 4) & 0xF0);
                    }

                    highBits[i >> 1] = (byte)(block1 | block2);
                }

                compound["Blocks"] = new NbtByteArray("Blocks", lowBits);
                compound["AddBlocks"] = new NbtByteArray("AddBlocks", highBits);
                NbtList paletteTag = new NbtList("Palette", NbtTagType.String);
                foreach (string paletteBlock in palette) {
                    paletteTag.Add(new NbtString(paletteBlock));
                }
                compound["Palette"] = paletteTag;

                inFile.SaveToFile(outPath, NbtCompression.GZip);
            }
        }
    }
}
