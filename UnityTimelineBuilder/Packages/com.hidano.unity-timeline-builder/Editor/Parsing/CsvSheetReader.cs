using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Hidano.UnityTimelineBuilder.Editor
{
    /// <summary>CSV/TSV の読み取りに失敗したことを表す例外。</summary>
    public sealed class SheetReadException : IOException
    {
        public SheetReadException(string message, Exception innerException = null)
            : base(message, innerException)
        {
        }
    }

    /// <summary>RFC 4180 の引用規則に対応した CSV/TSV リーダー。</summary>
    public sealed class CsvSheetReader
    {
        /// <summary>拡張子から区切り文字を判別し、空行を除く全行を読み取る。</summary>
        public IReadOnlyList<IReadOnlyList<string>> ReadAll(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("File path is required.", nameof(filePath));

            var extension = Path.GetExtension(filePath);
            char delimiter;
            if (string.Equals(extension, ".csv", StringComparison.OrdinalIgnoreCase))
                delimiter = ',';
            else if (string.Equals(extension, ".tsv", StringComparison.OrdinalIgnoreCase))
                delimiter = '\t';
            else
                throw new SheetReadException("Unsupported sheet extension for path: " + filePath);

            try
            {
                // StreamReader handles an optional UTF-8 BOM and validates the file as UTF-8.
                using (var reader = new StreamReader(filePath, new UTF8Encoding(false, true), true))
                    return Parse(reader.ReadToEnd(), delimiter, filePath);
            }
            catch (SheetReadException)
            {
                throw;
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException || exception is DecoderFallbackException)
            {
                throw new SheetReadException("Could not read sheet path: " + filePath, exception);
            }
        }

        private static IReadOnlyList<IReadOnlyList<string>> Parse(string text, char delimiter, string filePath)
        {
            var rows = new List<IReadOnlyList<string>>();
            var row = new List<string>();
            var field = new StringBuilder();
            var inQuotes = false;
            var afterClosingQuote = false;
            var fieldStarted = false;

            for (var index = 0; index < text.Length; index++)
            {
                var character = text[index];

                if (inQuotes)
                {
                    if (character != '"')
                    {
                        field.Append(character);
                        continue;
                    }

                    if (index + 1 < text.Length && text[index + 1] == '"')
                    {
                        field.Append('"');
                        index++;
                    }
                    else
                    {
                        inQuotes = false;
                        afterClosingQuote = true;
                    }

                    continue;
                }

                if (afterClosingQuote)
                {
                    if (character != delimiter && character != '\r' && character != '\n')
                        throw Malformed(filePath);
                    afterClosingQuote = false;
                }

                if (character == '"')
                {
                    if (fieldStarted || field.Length != 0)
                        throw Malformed(filePath);
                    inQuotes = true;
                    fieldStarted = true;
                }
                else if (character == delimiter)
                {
                    row.Add(field.ToString());
                    field.Clear();
                    fieldStarted = false;
                }
                else if (character == '\r' || character == '\n')
                {
                    AddRow(rows, row, field, fieldStarted);
                    fieldStarted = false;
                    if (character == '\r' && index + 1 < text.Length && text[index + 1] == '\n')
                        index++;
                }
                else
                {
                    field.Append(character);
                    fieldStarted = true;
                }
            }

            if (inQuotes)
                throw Malformed(filePath);

            AddRow(rows, row, field, fieldStarted);
            return rows;
        }

        private static void AddRow(List<IReadOnlyList<string>> rows, List<string> row, StringBuilder field, bool fieldStarted)
        {
            if (!fieldStarted && row.Count == 0)
                return;

            row.Add(field.ToString());
            rows.Add(new List<string>(row).AsReadOnly());
            row.Clear();
            field.Clear();
        }

        private static SheetReadException Malformed(string filePath)
        {
            return new SheetReadException("Malformed quoted field in sheet path: " + filePath);
        }
    }
}
