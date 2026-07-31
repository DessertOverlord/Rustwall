using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.GameContent;
using Vintagestory.ServerMods;

namespace Rustwall.Utils
{
    /// 
    /// Punch codes can be encoded as a 6-bit binary number:
    /// leftmost bit = true / false if the zone twelve hole is punched
    /// 2nd from left bit = true / false if the zone 11 hole is punched
    /// Both bits together = zone 0 hole is punched (this means 0 is encoded as 110000)
    /// bits 2-5 = binary encoding of the digits that are punched 
    /// (0 through '15', possible values 0b0000 through 0b1111)
    /// For numbers more than 9, you treat the "8" digit (bit 2) as a modifier.
    /// For instance, the '?' symbol is 111111:
    /// Zero row punched (11 on the left)
    /// Eight row punched (third 1 from the left)
    /// Remaining 3 bits give you 7 (4 + 2 + 1)
    /// So, you'd punch 0 + 7 + 8.
    ///
    public static class PunchCardUtils
    {
        public static readonly string PunchCardBackground =
        """
        ┌──┬──────────────────────────────────────────────────────────────────────────────────┐
        │PT│                                                                                  │
        │12│ -------------------------------------------------------------------------------- │
        │11│ -------------------------------------------------------------------------------- │
        │00│ 00000000000000000000000000000000000000000000000000000000000000000000000000000000 │
        │01│ 11111111111111111111111111111111111111111111111111111111111111111111111111111111 │
        │02│ 22222222222222222222222222222222222222222222222222222222222222222222222222222222 │
        │03│ 33333333333333333333333333333333333333333333333333333333333333333333333333333333 │
        │04│ 44444444444444444444444444444444444444444444444444444444444444444444444444444444 │
        │05│ 55555555555555555555555555555555555555555555555555555555555555555555555555555555 │
        │06│ 66666666666666666666666666666666666666666666666666666666666666666666666666666666 │
        │07│ 77777777777777777777777777777777777777777777777777777777777777777777777777777777 │
        │08│ 88888888888888888888888888888888888888888888888888888888888888888888888888888888 │
        │09│ 99999999999999999999999999999999999999999999999999999999999999999999999999999999 │
        │RF│ 0........1.........2.........3.........4.........5.........6.........7.........8 │
        └──┴──────────────────────────────────────────────────────────────────────────────────┘   
        """;

        /// <summary>
        /// Number of columns in the text. It's 87 chars long, 
        /// the two extra come from CRLF I assume
        /// </summary>
        public static readonly int RowLength = 89;
        /// <summary>
        /// Number of header columns on the left side
        /// </summary>
        public static readonly int ColOffset = 5;
        /// <summary>
        /// There's two rows of header before arriving at the data
        /// </summary>
        public static readonly int RowOffset = 2;
        /// <summary>
        /// Total number of template rows
        /// </summary>
        public static readonly int TotalRows = 16;
        /// <summary>
        /// How many characters wide is the total data space of this punchcard?
        /// </summary>
        public static readonly int DataSpaceLength = 80;
        /// <summary>
        /// How many characters tall is the total data space?
        /// </summary>
        public static readonly int DataSpaceHeight = 12;



        public static readonly char PunchedChar = '█';

        public static Dictionary<char, int> UTF16toPunchCodeLookupTable
        {
            get;

            private set;
        } = new()
        /// Credit to Claude for making this dictionary for me. 
        /// My eyes would have glazed over
        {
            // No punch
            { ' ', 0b000000 },

            // Zone-only punches
            { '&', 0b100000 }, // 12 only
            { '-', 0b010000 }, // 11 only

            // Digits 0–9
            { '0', 0b110000 }, // 0-row alone
            { '1', 0b000001 },
            { '2', 0b000010 },
            { '3', 0b000011 },
            { '4', 0b000100 },
            { '5', 0b000101 },
            { '6', 0b000110 },
            { '7', 0b000111 },
            { '8', 0b001000 },
            { '9', 0b001001 },

            // A–I: zone 12 + digit 1–9
            { 'A', 0b100001 },
            { 'B', 0b100010 },
            { 'C', 0b100011 },
            { 'D', 0b100100 },
            { 'E', 0b100101 },
            { 'F', 0b100110 },
            { 'G', 0b100111 },
            { 'H', 0b101000 },
            { 'I', 0b101001 },

            // 12 + 8 + digit (2–7): special characters
            { '¢', 0b101010 }, // 12-8-2
            { '.', 0b101011 }, // 12-8-3
            { '<', 0b101100 }, // 12-8-4
            { '(', 0b101101 }, // 12-8-5
            { '+', 0b101110 }, // 12-8-6
            { '|', 0b101111 }, // 12-8-7

            // J–R: zone 11 + digit 1–9
            { 'J', 0b010001 },
            { 'K', 0b010010 },
            { 'L', 0b010011 },
            { 'M', 0b010100 },
            { 'N', 0b010101 },
            { 'O', 0b010110 },
            { 'P', 0b010111 },
            { 'Q', 0b011000 },
            { 'R', 0b011001 },

            // 11 + 8 + digit (2–7): special characters
            { '!', 0b011010 }, // 11-8-2
            { '$', 0b011011 }, // 11-8-3
            { '*', 0b011100 }, // 11-8-4
            { ')', 0b011101 }, // 11-8-5
            { ';', 0b011110 }, // 11-8-6
            { '¬', 0b011111 }, // 11-8-7

            // Zone 0 + digit 1: slash
            { '/', 0b110001 },

            // S–Z: zone 0 + digit 2–9
            { 'S', 0b110010 },
            { 'T', 0b110011 },
            { 'U', 0b110100 },
            { 'V', 0b110101 },
            { 'W', 0b110110 },
            { 'X', 0b110111 },
            { 'Y', 0b111000 },
            { 'Z', 0b111001 },

            // 0 + 8 + digit (3–7): special characters
            { ',', 0b111011 }, // 0-8-3
            { '%', 0b111100 }, // 0-8-4
            { '_', 0b111101 }, // 0-8-5
            { '>', 0b111110 }, // 0-8-6
            { '?', 0b111111 }, // 0-8-7

            // No zone, 8 + digit (2–7): special characters
            { ':', 0b001010 }, // 8-2
            { '#', 0b001011 }, // 8-3
            { '@', 0b001100 }, // 8-4
            { '\'', 0b001101 }, // 8-5
            { '=', 0b001110 }, // 8-6
            { '"', 0b001111 }, // 8-7
        };

        public static int UTF16CharToPunchCode(char InUTF16)
        {
            try
            {
                return UTF16toPunchCodeLookupTable[InUTF16];
            }
            catch (KeyNotFoundException e)
            {
                Console.WriteLine("Attempted to look up unknown character to punch code translation");
            }

            return -1;
        }

        public static SortedList<int, int> UTF16StringToPunchCode(string InUTF16Str)
        {
            SortedList<int, int> output = new();

            foreach (char ch in InUTF16Str)
            {
                output.Add(output.Count, UTF16CharToPunchCode(ch));
            }

            return output;
        }

        /// <summary>
        /// Takes in a binary punchcode and deconstructs it into a list of the row positions where a punch should be added.
        /// </summary>
        /// <param name="code"></param>
        /// <returns></returns>
        public static List<int> UnpackPunchCode(int code)
        {
            List<int> output = new List<int>();

            if (code >= 0b110000)
            {
                output.Add(0);
                code -= 0b110000;
            }
            if (code >= 0b100000)
            {
                output.Add(12);
                code -= 0b100000;
            }
            if (code >= 0b010000)
            {
                output.Add(11);
                code -= 0b010000;
            }
            if (code >= 0b001010)
            {
                output.Add(8);
                code -= 0b001000;
            }
            if (code > 0b000000)
            {
                output.Add(code);
            }

            return output;
        } 

        /// <summary>
        /// Takes a string and returns the string encoded into the punch card format.
        /// </summary>
        /// <param name="inputString"></param>
        /// <param name="addPrintLine"></param>
        /// <returns></returns>
        public static string CreatePunchCard(string inputString, bool addPrintLine)
        {
            string upperString = inputString.ToUpper();
            SortedList<int, int> punchCodes = UTF16StringToPunchCode(upperString);
            char[] outputAsChars = PunchCardBackground.ToCharArray();
            /// Each line of the template is 87 characters long.
            /// The left headers are 5 columns before reaching the "data" part of the card.
            /// And there's two rows before you reach row 12 at the top.
            int dataStart = (RowOffset * RowLength) + ColOffset;

            if (addPrintLine)
            {
                int printLineStart = ((RowOffset - 1) * RowLength) + ColOffset;
                int counter = 0;
                foreach (char ch in upperString)
                {
                    outputAsChars[printLineStart + counter] = ch;
                    counter++;
                }
            }

            foreach (var kvp in punchCodes)
            {
                int code = kvp.Value;
                List<int> punches = UnpackPunchCode(code);

                foreach (int punch in punches)
                {
                    switch (punch)
                    {
                        case 12:
                            {
                                outputAsChars[dataStart] = PunchedChar;
                                continue;
                            }
                        case 11:
                            {
                                outputAsChars[dataStart + RowLength] = PunchedChar;
                                continue;
                            }
                        default:
                            {
                                outputAsChars[dataStart + ((punch + 2) * RowLength)] = PunchedChar;
                                continue;
                            }
                    }
                }
                
                dataStart++;
            }

            string result = new(outputAsChars);
            string saniResult = result.Replace("<", "&lt;").Replace(">", "&gt;");

            return saniResult;
        }

        public static string DecodePunchCard(string inputCard)
        {
            string output = "";
            char[] inputCardCharArr = inputCard.ToCharArray();
            Dictionary<int, char> InvertedLookupTable = UTF16toPunchCodeLookupTable.ToDictionary(x => x.Value, x => x.Key);

            /// First validate that this is a valid template by checking its total size
            if (inputCard.Count() != (TotalRows * RowLength) + 1)
            {
                return "";
            }

            int dataStart = (RowLength * RowOffset) + ColOffset;

            for (int i = 0; i < DataSpaceLength; i++)
            {
                int NumPunchesFound = 0;
                int DecodedCharacterAsBinary = 0b000000;

                /// Offset by two to account for the fact that 12 and 11 come before 0
                for (int j = -2; j < DataSpaceHeight - 2; j++)
                {
                    /// Max is 3 punches; no need to iterate if we've already found 3 punches.
                    if (NumPunchesFound >= 3)
                    {
                        break;
                    }

                    if (inputCardCharArr[dataStart + i + ((j + 2) * RowLength)] == PunchedChar)
                    {
                        NumPunchesFound++;

                        switch (j)
                        {
                            case -2:
                                {
                                    DecodedCharacterAsBinary += 0b100000;
                                    break;
                                }
                            case -1:
                                {
                                    DecodedCharacterAsBinary += 0b010000;
                                    break;
                                }
                            case 0:
                                {
                                    DecodedCharacterAsBinary += 0b110000;
                                    break;
                                }
                            default:
                                {
                                    DecodedCharacterAsBinary += j;
                                    break;
                                }
                        }
                    }
                }

                /// This is safe because every key and value is unique in our lookup dictionary
                output += InvertedLookupTable[DecodedCharacterAsBinary];
            }

            return output;
        }
    }
}
