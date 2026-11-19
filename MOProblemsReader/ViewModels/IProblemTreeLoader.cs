namespace MOProblemsReader.ViewModels;

/// <summary>
/// 题库赛事节点的数据来源，负责从本地仓库读取某个分类下的全部赛事及其年份。
/// </summary>
public interface IProblemTreeLoader
{
    /// <summary>
    /// 读取指定分类下的全部赛事节点（每个赛事节点已预填年份子节点）。
    /// </summary>
    /// <param name="categoryName">分类目录名，如 <c>china</c>。</param>
    /// <returns>赛事节点列表；分类目录存在但没有任何赛事时返回空列表。</returns>
    /// <exception cref="ArgumentException">分类目录名不在已知分类中。</exception>
    /// <exception cref="DirectoryNotFoundException">分类目录不存在或无法访问时抛出。</exception>
    Task<IReadOnlyList<ProblemNodeViewModel>> LoadCompetitionsAsync(string categoryName);
}
