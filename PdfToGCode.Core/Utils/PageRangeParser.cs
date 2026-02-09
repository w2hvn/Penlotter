using System;
using System.Collections.Generic;
using System.Linq;

namespace PdfToGCode.Core.Utils
{
    public static class PageRangeParser
    {
        public static List<int> Parse(string range, int totalPages)
        {
            var pages = new HashSet<int>();
            if (string.IsNullOrWhiteSpace(range)) return new List<int>();

            var parts = range.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts)
            {
                var trimmed = part.Trim();
                if (trimmed.Contains("-"))
                {
                    // Range e.g. 1-5
                    var rangeParts = trimmed.Split('-');
                    if (rangeParts.Length == 2 &&
                        int.TryParse(rangeParts[0], out int start) &&
                        int.TryParse(rangeParts[1], out int end))
                    {
                        int s = Math.Min(start, end);
                        int e = Math.Max(start, end);
                        for (int i = s; i <= e; i++)
                        {
                            if (i >= 1 && i <= totalPages) pages.Add(i);
                        }
                    }
                }
                else
                {
                    // Single number e.g. 5
                    if (int.TryParse(trimmed, out int p))
                    {
                        if (p >= 1 && p <= totalPages) pages.Add(p);
                    }
                }
            }

            return pages.OrderBy(x => x).ToList();
        }
    }
}
