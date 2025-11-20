using ModernWpf.Controls;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace VRisingServerManager;

public partial class ModConfigEditor : Window
{
    private readonly string _configFilePath;
    private string _originalContent;
    private bool _isModified => !string.Equals(GetCurrentContent(), _originalContent, StringComparison.Ordinal); // 判断内容是否被修改

    private string _currentWord = string.Empty;
    private int _currentWordStart = 0;

    private readonly List<string> _keywords = new List<string>
    {
        "true", "false",
        
        "Enable", "Disable", "Max", "Min", "Cost", "Time",
        "Default", "Personal", "Global", "Expire", "Days",
        "Lifetime", "Cooldown", "Request", "Expiration",
        "Maximum", "Count", "Limit", "Range", "Distance",
        "Rate", "Amount", "Value", "Name", "GUID", "ID",
        
        "General", "Costs", "Prefabs", "Timers", "Settings",
        "Permissions", "Features", "UI", "Network", "Debug"
    };

    // 搜索字段
    private string _searchText = string.Empty;
    private TextRange _currentMatchRange;
    private List<TextRange> _allMatches = new List<TextRange>();
    private int _currentMatchIndex = -1;
    private readonly Brush _highlightBrush = new SolidColorBrush(Color.FromRgb(255, 255, 0)); // 黄色高亮
    private readonly Brush _originalBackgroundBrush = Brushes.Transparent;

    private readonly Regex _groupRegex = new Regex(@"^\[(?<GroupName>.+)\]$");
    private readonly Regex _commentRegex = new Regex(@"^#(?<Comment>.+)$");
    private readonly Regex _configRegex = new Regex(@"^(?<Key>\w+)\s*=\s*(?<Value>.+)$");
    private readonly Regex _defaultValueRegex = new Regex(@"# Default value: (?<DefaultValue>.+)");
    private readonly Regex _wordRegex = new Regex(@"[\w]+$");

    public ModConfigEditor(string configFilePath, string initialContent)
    {
        InitializeComponent();
        _configFilePath = configFilePath;
        LoadConfigContent(initialContent);
        _originalContent = GetCurrentContent(); // 初始化原始内容
        Title = $"配置文件编辑器 - {Path.GetFileName(configFilePath)}";

        // 注册快捷键Ctrl+F
        var keyGesture = new KeyGesture(Key.F, ModifierKeys.Control);
        var command = new RoutedCommand();
        command.InputGestures.Add(keyGesture);
        CommandBindings.Add(new CommandBinding(command, (s, e) => ShowSearchPanel()));

        // 监听文本变化，标记修改状态
        ConfigRichTextBox.TextChanged += (s, e) =>
        {
            if (_isModified)
                Title = $"配置文件编辑器 - {Path.GetFileName(configFilePath)} *";
            else
                Title = $"配置文件编辑器 - {Path.GetFileName(configFilePath)}";
        };

    }

    private void LoadConfigContent(string content)
    {
        var document = new FlowDocument();
        string[] lines = content.Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);

        foreach (string line in lines)
        {
            var paragraph = new Paragraph();
            paragraph.Margin = new Thickness(5);
            string trimmedLine = line.Trim();

            if (_groupRegex.IsMatch(trimmedLine))
            {
                var groupRun = new Run(line) { Style = (Style)FindResource("GroupStyle") };
                paragraph.Inlines.Add(groupRun);
            }
            else if (trimmedLine.StartsWith("#"))
            {
                if (_defaultValueRegex.IsMatch(trimmedLine))
                {
                    var defaultRun = new Run(line) { Style = (Style)FindResource("DefaultValueStyle") };
                    paragraph.Inlines.Add(defaultRun);
                }
                else
                {
                    var commentRun = new Run(line) { Style = (Style)FindResource("CommentStyle") };
                    paragraph.Inlines.Add(commentRun);
                }
            }
            else if (_configRegex.IsMatch(trimmedLine))
            {
                var match = _configRegex.Match(trimmedLine);
                var keyRun = new Run($"{match.Groups["Key"].Value} = ")
                { Style = (Style)FindResource("KeyStyle") };
                var valueRun = new Run(match.Groups["Value"].Value)
                { Style = (Style)FindResource("ValueStyle") };

                paragraph.Inlines.Add(keyRun);
                paragraph.Inlines.Add(valueRun);
                paragraph.Inlines.Add("\r\n");
            }
            else
            {
                paragraph.Inlines.Add(new Run(line));
            }
            document.Blocks.Add(paragraph);
        }

        ConfigRichTextBox.Document = document;
    }

    // 处理文本变化，触发自动完成
    private void ConfigRichTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (ConfigRichTextBox == null || AutoCompletePopup == null || SuggestionsListBox == null)
            return;

        TextPointer caret = ConfigRichTextBox.CaretPosition;
        if (caret == null)
            return;

        string currentLine = GetTextFromPositionToStartOfLine(caret);
        if (string.IsNullOrEmpty(currentLine))
            return;

        // 提取当前正在输入的单词
        Match match = _wordRegex.Match(currentLine);
        if (match.Success && match.Length >= 2)
        {
            _currentWord = match.Value;
            _currentWordStart = currentLine.Length - match.Length;

            // 查找匹配的建议
            var suggestions = _keywords
                .Where(k => k.StartsWith(_currentWord, StringComparison.OrdinalIgnoreCase))
                .OrderBy(k => k)
                .ToList();

            if (suggestions.Any())
            {
                ShowSuggestions(suggestions, caret);
                return;
            }
        }

        // 没有匹配项，隐藏建议列表
        AutoCompletePopup.IsOpen = false;
    }

    // 显示联想建议
    private void ShowSuggestions(List<string> suggestions, TextPointer caret)
    {
        if (SuggestionsListBox == null || AutoCompletePopup == null || caret == null)
            return;

        // 设置建议列表内容
        SuggestionsListBox.ItemsSource = suggestions;
        SuggestionsListBox.SelectedIndex = 0;

        // 计算建议框位置
        try
        {
            Rect rect = caret.GetCharacterRect(LogicalDirection.Forward);
            Point position = ConfigRichTextBox.TransformToAncestor(this)
                .Transform(new Point(rect.X, rect.Y + rect.Height));

            AutoCompletePopup.PlacementTarget = ConfigRichTextBox;
            AutoCompletePopup.HorizontalOffset = position.X;
            AutoCompletePopup.VerticalOffset = position.Y;
            AutoCompletePopup.IsOpen = true;
        }
        catch (Exception ex)
        {
            // 位置计算失败时不显示建议框
            AutoCompletePopup.IsOpen = false;
        }
    }

    // Tab补全
    private void ConfigRichTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Tab && AutoCompletePopup.IsOpen)
        {
            CompleteWord();
            e.Handled = true;
        }
        else if (AutoCompletePopup.IsOpen)
        {
            if (e.Key == Key.Down)
            {
                int nextIndex = SuggestionsListBox.SelectedIndex + 1;
                if (nextIndex < SuggestionsListBox.Items.Count)
                    SuggestionsListBox.SelectedIndex = nextIndex;
                e.Handled = true;
            }
            else if (e.Key == Key.Up)
            {
                int prevIndex = SuggestionsListBox.SelectedIndex - 1;
                if (prevIndex >= 0)
                    SuggestionsListBox.SelectedIndex = prevIndex;
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                AutoCompletePopup.IsOpen = false;
                e.Handled = true;
            }
        }
    }

    // 鼠标点击选择建议
    private void SuggestionsListBox_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        CompleteWord();
    }

    // 补全当前单词
    private void CompleteWord()
    {
        if (SuggestionsListBox.SelectedItem is string selectedWord)
        {
            TextPointer caret = ConfigRichTextBox.CaretPosition;

            TextPointer start = caret.GetPositionAtOffset(-_currentWord.Length);
            if (start != null)
            {
                var range = new TextRange(start, caret);
                range.Text = "";
            }
             
            ConfigRichTextBox.CaretPosition.InsertTextInRun(selectedWord);
            ConfigRichTextBox.CaretPosition = caret.GetPositionAtOffset(+selectedWord.Length);

            AutoCompletePopup.IsOpen = false;
        }
    }

    private string GetTextFromPositionToStartOfLine(TextPointer position)
    {
        TextPointer startOfLine = position;
        while (startOfLine.GetPointerContext(LogicalDirection.Backward) != TextPointerContext.None &&
               startOfLine.GetTextInRun(LogicalDirection.Backward) != "\r\n")
        {
            startOfLine = startOfLine.GetNextInsertionPosition(LogicalDirection.Backward);
            if (startOfLine == null) break;
        }

        if (startOfLine == null) startOfLine = position.DocumentStart;

        var range = new TextRange(startOfLine, position);
        return range.Text;
    }

    private string GetCurrentContent()
    {
        var range = new TextRange(
            ConfigRichTextBox.Document.ContentStart,
            ConfigRichTextBox.Document.ContentEnd
        );
        return range.Text;
    }

    private void ShowSearchPanel()
    {
        //SearchPanel.Visibility = Visibility.Visible;
        //SearchTextBox.Focus();
        //SearchTextBox.SelectAll();
    }

    // 保存改变
    private async Task<bool> SaveChanges()
    {
        try
        {
            string content = GetCurrentContent();
            File.WriteAllText(_configFilePath, content);
            _originalContent = content;

            var saveSuccessDialog = new ContentDialog
            {
                Owner = this,
                Title = "保存配置文件",
                Content = "配置文件保存成功！",
                PrimaryButtonText = "好的",
                DefaultButton = ContentDialogButton.Primary
            };

            await saveSuccessDialog.ShowAsync();
            return true;
        }
        catch (Exception ex)
        {
            var saveFailDialog = new ContentDialog
            {
                Owner = this,
                Title = "保存配置文件",
                Content = $"保存失败！\r错误 {ex.Message}",
                PrimaryButtonText = "好的",
                DefaultButton = ContentDialogButton.Primary
            };
            await saveFailDialog.ShowAsync();
            return false;
        }
    }

    private async void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (await SaveChanges())
        {
            Close();
        }
    }

    private async void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isModified)
        {
            // 创建确认对话框
            var yesNoDialog = new ContentDialog
            {
                Owner = this,
                Title = "确认关闭",
                Content = "配置文件已修改，是否保存更改？\n选择“否”将丢弃所有修改。",
                PrimaryButtonText = "是",  
                SecondaryButtonText = "否",
                CloseButtonText = "取消",   
                DefaultButton = ContentDialogButton.Close
            };
            var result = await yesNoDialog.ShowAsync();

            // 根据结果处理
            switch (result)
            {
                case ContentDialogResult.Primary:
                    if (!await SaveChanges())
                        break;
                    else
                        Close();
                        break;
                case ContentDialogResult.Secondary:
                    Close();
                    break;
                case ContentDialogResult.None:
                    break;
            }
        }
        Close();
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
    }
}
