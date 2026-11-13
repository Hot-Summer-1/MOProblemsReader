using System;
using System.Collections.Generic;
using System.Text;

namespace ProblemViewer
{
    public class CompetitionInfo
    {
        public string? name { get; set; }
        public required string display_name { get; set; }
        public required List<YearInfo> years { get; set; } = new();
    }

    public class YearInfo
    {
        public required string name { get; set; }
        public required int count { get; set; }
        public required bool no_problem_content { get; set; }
        public required int begin_index { get; set; }
    }
}
