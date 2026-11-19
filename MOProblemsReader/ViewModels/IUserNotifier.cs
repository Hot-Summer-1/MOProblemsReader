namespace MOProblemsReader.ViewModels;

/// <summary>
/// 视图层提示能力抽象，使视图模型不直接依赖 <c>MessageBox</c>。
/// </summary>
public interface IUserNotifier
{
    /// <summary>
    /// 以错误提示框告知用户一次失败。
    /// </summary>
    /// <param name="title">提示框标题。</param>
    /// <param name="message">提示框正文。</param>
    Task ShowErrorAsync(string title, string message);
}
