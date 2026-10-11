using System;
using System.Collections.Generic;
using System.Text;

namespace MonitorControl
{
    public class SourceSymbol
    {
        public required string Name { get; set; }

        public UInt16 Value { get; set; }
    }

    public class SourceSymbolTable
    {
        public Dictionary<string, SourceSymbol> Symbols { get; set; } = new Dictionary<string, SourceSymbol>();

        public SourceSymbolTable() { }

        public void Add(SourceSymbol symbol)
        {
            Symbols[symbol.Name] = symbol;
        }

        public SourceSymbol? GetSymbol(string name)
        {
            if (Symbols.TryGetValue(name, out var symbol))
            {
                return symbol;
            }

            return null;
        }

        public IEnumerable<SourceSymbol> GetSymbolsFromValue(UInt16 value)
        {
            return Symbols.Where(s => s.Value.Value == value).Select(s => s.Value).ToArray();
        }

        public void ClearSymbols()
        {
            Symbols.Clear();
        }
    }

    public static class SymLoader
    {
        public static (bool success, int countSymbolsAdded, int countLinesIgnored) LoadSymbolsFromFile(string filePath, SourceSymbolTable symbolTable)
        {
            int countSymbolsAdded = 0;
            int countLinesIgnored = 0;
            bool success = false;

            try
            {
                foreach (var line in System.IO.File.ReadLines(filePath))
                {
                    var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 3)
                    {
                        if (parts[0] == "al")
                        {
                            if (parts[1].Length > 2 && parts[1].Substring(0, 2).ToLower() == "c:")
                            {
                                if (UInt16.TryParse(parts[1].Substring(2), System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out var value))
                                {
                                    var name = (parts[2].StartsWith('.')) ? parts[2].Substring(1) : parts[2];
                                    var symbol = new SourceSymbol { Name = name, Value = value };
                                    symbolTable.Add(symbol);
                                    countSymbolsAdded++;
                                    continue;
                                }

                            }
                        }
                    }

                    countLinesIgnored++;
                }

                success = true;
            } catch (Exception ex) 
            {
                Console.WriteLine($"Error occurred while loading symbols from file '{filePath}': {ex.Message}");
            }

            return (success, countSymbolsAdded, countLinesIgnored);
        }
    }
}
