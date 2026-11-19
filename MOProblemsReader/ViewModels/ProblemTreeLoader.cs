using System.IO;
using System.Text.Json;
using ProblemViewer;
using Serilog;

namespace MOProblemsReader.ViewModels;

/// <summary>
/// 从本地题库仓库的 <c>problems\&lt;分类&gt;\&lt;赛事&gt;\info.json</c> 读取赛事与年份。
/// 不引用任何 WPF 类型，便于脱离界面测试。
/// </summary>
public sealed class ProblemTreeLoader : IProblemTreeLoader
{
    /// <summary>
    /// 已知的分类目录名，与仓库目录结构约定保持一致。
    /// </summary>
    private static readonly string[] KnownCategories = { "china", "abroad", "university", "original" };

    /// <summary>
    /// info.json 的序列化选项：仓库字段名多为 snake_case，保持大小写不敏感以兼容。
    /// </summary>
    private static readonly JsonSerializerOptions SerializerOptions = new() { PropertyNameCaseInsensitive = true };

    /// <inheritdoc />
    public Task<IReadOnlyList<ProblemNodeViewModel>> LoadCompetitionsAsync(string categoryName)
    {
        if (Array.IndexOf(KnownCategories, categoryName) < 0)
        {
            throw new ArgumentException($"未知的题目分类: {categoryName}", nameof(categoryName));
        }

        // 读盘与反序列化较重，放到线程池，避免展开节点时卡住 UI 线程
        return Task.Run<IReadOnlyList<ProblemNodeViewModel>>(() => LoadCompetitionsCore(categoryName));
    }

    /// <summary>
    /// 同步读取并构造赛事节点，单个赛事失败只记日志并跳过，保证一个坏文件不影响整个分类。
    /// </summary>
    /// <param name="categoryName">分类目录名。</param>
    /// <returns>赛事节点列表；分类目录存在但没有任何赛事时返回空列表。</returns>
    /// <exception cref="DirectoryNotFoundException">分类目录不存在或无法访问时抛出。</exception>
    /// <exception cref="UnauthorizedAccessException">没有读取目录的权限时抛出。</exception>
    private static IReadOnlyList<ProblemNodeViewModel> LoadCompetitionsCore(string categoryName)
    {
        var categoryPath = Path.Combine(Settings.LocalRepositoryPath, "problems", categoryName);

        List<string> competitionDirs;
       
            // EnumerateDirectories 是延迟枚举，不物化的话异常会推迟到 foreach 里，
            // 与「单个赛事失败跳过」混在一起，无法区分是外层故障还是坏文件；
            // 同时按目录名排序，保证每次加载顺序稳定（直接枚举文件系统时顺序不保证）
            competitionDirs = Directory.EnumerateDirectories(categoryPath)
                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                .ToList();
        
        

        var competitions = new List<ProblemNodeViewModel>();

        foreach (var competitionDir in competitionDirs)
        {
            var infoPath = Path.Combine(competitionDir, "info.json");
            if (!File.Exists(infoPath))
            {
                continue;
            }

            try
            {
                var info = JsonSerializer.Deserialize<CompetitionInfo>(File.ReadAllText(infoPath), SerializerOptions);
                var name = info!.name;
                var displayName = string.IsNullOrWhiteSpace(info.display_name) ? name : info.display_name;

                var competition = new ProblemNodeViewModel(ProblemNodeKind.Competition, name!, displayName!)
                {
                    // 年份随赛事信息一并给出，构造时即预填，因此展开赛事节点无需再读盘
                    HasLoaded = true
                };

                foreach (var year in info.years)
                {
                    competition.Children.Add(new ProblemNodeViewModel(ProblemNodeKind.Year, year.name, year.name)
                    {
                        Parent = competition,
                        HasLoaded = true
                    });
                }

                competitions.Add(competition);
            }
            catch (Exception ex)
            {
                // 单个赛事目录数据损坏不应拖垮整个分类，记录原始异常后跳过
                Log.Warning(ex, "Failed to load competition info. Competition: {name}", Path.GetFileName(competitionDir.TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar)));
            }
        }

        return competitions;
    }
}
