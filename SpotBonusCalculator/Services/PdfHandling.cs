using System.Text.RegularExpressions;
using Tabula;
using Tabula.Extractors;
using UglyToad.PdfPig;

namespace SpotBonusCalculator.Services;

public class PdfHandling
{
    public TimecardData ReadTimecard(byte[] pdfBytes)
    {
        var data = new TimecardData();
        using var document = PdfDocument.Open(pdfBytes, new ParsingOptions { ClipPaths = true });
        var lattice = new SpreadsheetExtractionAlgorithm();

        for (var p = 1; p <= document.NumberOfPages; p++)
        {
            var page = ObjectExtractor.ExtractPage(document, p);
            var table = lattice.Extract(page).FirstOrDefault(IsPunchTable);
            if (table == null)
                continue;

            bool pastHeader = false;

            foreach (var row in table.Rows)
            {
                var cells = row.Select(c => c.GetText().Trim()).ToList();
                
                // Dont extract Totals
                if (cells.Any(c => c.StartsWith("Totals")))
                    break;

                if (IsHeaderRow(cells))
                {
                    // Add headers once
                    if (data.Rows.Count == 0)
                        data.Rows.Add(cells);
                    pastHeader = true;
                    continue;
                }

                if (!pastHeader)
                {
                    var match = Regex.Match(cells[0], @"^[^,]+,\s*\S+");
                    if (match.Success)
                        data.EmployeeName = match.Value;
                    continue;
                }
                
                data.Rows.Add(cells);
            }
        }
        
        return data;
    }
    

    private static bool IsPunchTable(Table table)
    {
        return table.Rows.Any(r => IsHeaderRow(r.Select(c => c.GetText().Trim()).ToList()));
    }

    private static bool IsHeaderRow(List<string> cells)
    {
        return cells.Contains("Date") && cells.Contains("In") && cells.Contains("Out");
    }
}

public class TimecardData
{
    public string EmployeeName { get; set; } = "Unknown";
    public List<List<string>> Rows { get; set; } = new();
}