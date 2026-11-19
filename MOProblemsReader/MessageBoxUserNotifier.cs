using System.Windows;
using MOProblemsReader.ViewModels;

namespace MOProblemsReader;

/// <summary>
/// 用 WPF 的 <c>MessageBox</c> 实现 <see cref="IUserNotifier"/>，作为主窗口的提示出口。
/// </summary>
public sealed class MessageBoxUserNotifier : IUserNotifier
{
    private readonly Window _owner;

    /// <summary>
    /// 创建提示器。
    /// </summary>
    /// <param name="owner">提示框的属主窗口，用于居中并保持模态关系。</param>
    public MessageBoxUserNotifier(Window owner)
    {
        _owner = owner;
    }

    /// <inheritdoc />
    public Task ShowErrorAsync(string title, string message)
    {
        MessageBox.Show(_owner, message, title, MessageBoxButton.OK, MessageBoxImage.Error);
        return Task.CompletedTask;
    }
}
