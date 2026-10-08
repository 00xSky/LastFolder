using System;
using System.Collections.Generic;
using LastFolder.Models;

namespace LastFolder.Services
{
    /// <summary>Dev only: made-up items for the --demo switch (README screenshot).</summary>
    internal static class DemoItems
    {
        public static List<RecentItem> Create()
        {
            var now = DateTime.Now;
            var yesterday = now.Date.AddDays(-1);

            return new List<RecentItem>
            {
                File(@"C:\Users\demo\Documents\Finance\Q3 Report.xlsx", now.AddSeconds(-20), 254_310),
                Folder(@"C:\Users\demo\Projects", now.AddMinutes(-12)),
                File(@"C:\Users\demo\Projects\atlas\docs\design-notes.md", now.AddMinutes(-37), 14_820),
                File(@"C:\Users\demo\Documents\Meeting Notes.docx", now.AddHours(-1).AddMinutes(-25), 41_250),
                File(@"C:\Users\demo\Downloads\invoice_2026.pdf", now.AddHours(-2).AddMinutes(-40), 186_400),
                Folder(@"C:\Users\demo\Pictures\Screenshots", yesterday.AddHours(21).AddMinutes(10)),
                File(@"C:\Users\demo\Documents\Finance\budget-2026.xlsx", yesterday.AddHours(18).AddMinutes(40), 98_730),
                File(@"C:\Users\demo\Documents\contract_draft.pdf", yesterday.AddHours(11).AddMinutes(15), 2_457_600),
                Folder(@"C:\Users\demo\Downloads", now.Date.AddDays(-3).AddHours(9).AddMinutes(30)),
                File(@"C:\Users\demo\Projects\atlas\release-checklist.md", now.Date.AddDays(-6).AddHours(16).AddMinutes(5), 3_120),
            };
        }

        private static RecentItem File(string path, DateTime time, long size) => new(path, ItemKind.File, time, size);

        private static RecentItem Folder(string path, DateTime time) => new(path, ItemKind.Folder, time, null);
    }
}
