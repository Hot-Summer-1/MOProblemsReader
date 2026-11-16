namespace ProblemViewer
{
    /// <summary>
    /// 一个赛事的信息，对应题库仓库中 <c>problems\&lt;分类&gt;\&lt;赛事目录&gt;\info.json</c> 的内容。
    /// 属性名刻意与 JSON 字段保持一致（snake_case），不遵循 C# 命名约定。
    /// </summary>
    public class CompetitionInfo
    {
        /// <summary>
        /// 赛事目录名，加载时用目录名回填（JSON 中通常不提供）。
        /// </summary>
        public string? name { get; set; }

        /// <summary>
        /// 界面上显示的赛事名称。
        /// </summary>
        public required string display_name { get; set; }

        /// <summary>
        /// 该赛事包含的年份条目。
        /// </summary>
        public required List<YearInfo> years { get; set; } = new();
    }

    /// <summary>
    /// 赛事下某一年份（届次）的题目集合信息。
    /// </summary>
    public class YearInfo
    {
        /// <summary>
        /// 年份条目的显示名称。
        /// </summary>
        public required string name { get; set; }

        /// <summary>
        /// 该年份下的题目数量。
        /// </summary>
        public required int count { get; set; }

        /// <summary>
        /// 为 <c>true</c> 表示该年份只有题目清单而无题目正文，不应尝试打开题目内容。
        /// </summary>
        public required bool no_problem_content { get; set; }

        /// <summary>
        /// 该年份第一道题在整个赛事题目序列中的起始序号，用于定位题目文件。
        /// </summary>
        public required int begin_index { get; set; }
    }
}
