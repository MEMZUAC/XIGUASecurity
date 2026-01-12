using Microsoft.UI.Text;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Windows.UI;
using Windows.UI.Text;
using TextGetOptions = Microsoft.UI.Text.TextGetOptions;

namespace Code_Editor
{
    public class SyntaxHighlighter
    {
        private readonly Dictionary<string, List<SyntaxRule>> _languageRules;
        
        public SyntaxHighlighter()
        {
            _languageRules = new Dictionary<string, List<SyntaxRule>>();
            InitializeLanguageRules();
        }
        
        private void InitializeLanguageRules()
        {
            // C# 语法规则
            _languageRules["C#"] = new List<SyntaxRule>
            {
                // 关键字
                new SyntaxRule(@"\b(abstract|as|base|bool|break|byte|case|catch|char|checked|class|const|continue|decimal|default|delegate|do|double|else|enum|event|explicit|extern|false|finally|fixed|float|for|foreach|goto|if|implicit|in|int|interface|internal|is|lock|long|namespace|new|null|object|operator|out|override|params|private|protected|public|readonly|ref|return|sbyte|sealed|short|sizeof|stackalloc|static|string|struct|switch|this|throw|true|try|typeof|uint|ulong|unchecked|unsafe|ushort|using|virtual|void|volatile|while|var|async|await|nameof)\b", 
                               Color.FromArgb(255, 0, 0, 255)),
                
                // 注释
                new SyntaxRule(@"//.*$", Color.FromArgb(255, 0, 128, 0)),
                new SyntaxRule(@"/\*[\s\S]*?\*/", Color.FromArgb(255, 0, 128, 0)),
                
                // 字符串
                new SyntaxRule(@"""([^""\\]|\\.)*""", Color.FromArgb(255, 165, 42, 42)),
                new SyntaxRule(@"'([^'\\]|\\.)*'", Color.FromArgb(255, 165, 42, 42)),
                
                // 数字
                new SyntaxRule(@"\b\d+\.?\d*([eE][+-]?\d+)?[fFdDmM]?\b", Color.FromArgb(255, 255, 0, 0)),
                
                // 预处理器指令
                new SyntaxRule(@"^\s*#.*$", Color.FromArgb(255, 128, 128, 128))
            };
            
            // JavaScript 语法规则
            _languageRules["JavaScript"] = new List<SyntaxRule>
            {
                // 关键字
                new SyntaxRule(@"\b(break|case|catch|class|const|continue|debugger|default|delete|do|else|export|extends|finally|for|function|if|import|in|instanceof|let|new|return|super|switch|this|throw|try|typeof|var|void|while|with|yield|async|await)\b", 
                               Color.FromArgb(255, 0, 0, 255)),
                
                // 注释
                new SyntaxRule(@"//.*$", Color.FromArgb(255, 0, 128, 0)),
                new SyntaxRule(@"/\*[\s\S]*?\*/", Color.FromArgb(255, 0, 128, 0)),
                
                // 字符串
                new SyntaxRule(@"""([^""\\]|\\.)*""", Color.FromArgb(255, 165, 42, 42)),
                new SyntaxRule(@"'([^'\\]|\\.)*'", Color.FromArgb(255, 165, 42, 42)),
                new SyntaxRule(@"`([^`\\]|\\.)*`", Color.FromArgb(255, 165, 42, 42)),
                
                // 数字
                new SyntaxRule(@"\b\d+\.?\d*([eE][+-]?\d+)?[fF]?\b", Color.FromArgb(255, 255, 0, 0)),
                
                // 正则表达式
                new SyntaxRule(@"/[^/\n]*\w*[gimy]*", Color.FromArgb(255, 128, 0, 128))
            };
            
            // Python 语法规则
            _languageRules["Python"] = new List<SyntaxRule>
            {
                // 关键字
                new SyntaxRule(@"\b(and|as|assert|break|class|continue|def|del|elif|else|except|exec|finally|for|from|global|if|import|in|is|lambda|not|or|pass|print|raise|return|try|while|with|yield|async|await|nonlocal)\b", 
                               Color.FromArgb(255, 0, 0, 255)),
                
                // 注释
                new SyntaxRule(@"#.*$", Color.FromArgb(255, 0, 128, 0)),
                
                // 字符串
                new SyntaxRule(@"""([^""\\]|\\.)*""", Color.FromArgb(255, 165, 42, 42)),
                new SyntaxRule(@"'([^'\\]|\\.)*'", Color.FromArgb(255, 165, 42, 42)),
                new SyntaxRule(@"'''[\s\S]*?'''", Color.FromArgb(255, 165, 42, 42)),
                new SyntaxRule(@"""[\s\S]*?""", Color.FromArgb(255, 165, 42, 42)),
                
                // 数字
                new SyntaxRule(@"\b\d+\.?\d*([eE][+-]?\d+)?[jJ]?\b", Color.FromArgb(255, 255, 0, 0)),
                
                // 布尔值
                new SyntaxRule(@"\b(True|False|None)\b", Color.FromArgb(255, 128, 0, 128))
            };
            
            // XML 语法规则
            _languageRules["XML"] = new List<SyntaxRule>
            {
                // 标签
                new SyntaxRule(@"<[^>]+>", Color.FromArgb(255, 0, 0, 255)),
                
                // 属性
                new SyntaxRule(@"\w+\s*=", Color.FromArgb(255, 255, 0, 0)),
                
                // 属性值
                new SyntaxRule(@"""([^""]*)""", Color.FromArgb(255, 165, 42, 42)),
                new SyntaxRule(@"'([^']*)'", Color.FromArgb(255, 165, 42, 42)),
                
                // 注释
                new SyntaxRule(@"<!--[\s\S]*?-->", Color.FromArgb(255, 0, 128, 0)),
                
                // CDATA
                new SyntaxRule(@"<!\[CDATA\[[\s\S]*?\]\]>", Color.FromArgb(255, 128, 128, 128))
            };
            
            // JSON 语法规则
            _languageRules["JSON"] = new List<SyntaxRule>
            {
                // 属性名
                new SyntaxRule(@"""[^""]*""(?=\s*:)", Color.FromArgb(255, 0, 0, 255)),
                
                // 字符串值
                new SyntaxRule(@"""([^""\\]|\\.)*""", Color.FromArgb(255, 165, 42, 42)),
                
                // 数字
                new SyntaxRule(@"-?\d+\.?\d*([eE][+-]?\d+)?", Color.FromArgb(255, 255, 0, 0)),
                
                // 布尔值和null
                new SyntaxRule(@"\b(true|false|null)\b", Color.FromArgb(255, 128, 0, 128))
            };
        }
        
        public async Task ApplyHighlightingAsync(RichEditBox richEditBox, string language)
        {
            if (!_languageRules.ContainsKey(language))
                return;
                
            // 获取文档
            var document = richEditBox.Document;
            
            // 获取文本内容
            string text;
            document.GetText(TextGetOptions.None, out text);
            
            // 保存当前选择位置
            var selection = document.Selection;
            int startPosition = selection.StartPosition;
            int endPosition = selection.EndPosition;
            bool isCollapsed = selection.Length == 0;
            
            // 清除所有格式
            var range = document.GetRange(0, text.Length);
            range.CharacterFormat.ForegroundColor = Color.FromArgb(255, 0, 0, 0); // 重置为黑色
            
            // 应用语法高亮
            var rules = _languageRules[language];
            foreach (var rule in rules)
            {
                var matches = rule.Pattern.Matches(text);
                foreach (Match match in matches)
                {
                    var highlightRange = document.GetRange(match.Index, match.Index + match.Length);
                    highlightRange.CharacterFormat.ForegroundColor = rule.Color;
                }
            }
            
            // 恢复选择位置
            if (isCollapsed)
            {
                document.Selection.SetRange(startPosition, startPosition);
            }
            else
            {
                document.Selection.SetRange(startPosition, endPosition);
            }
        }
    }
    
    public class SyntaxRule
    {
        public Regex Pattern { get; }
        public Color Color { get; }
        
        public SyntaxRule(string pattern, Color color)
        {
            Pattern = new Regex(pattern, RegexOptions.Compiled | RegexOptions.Multiline);
            Color = color;
        }
    }
}