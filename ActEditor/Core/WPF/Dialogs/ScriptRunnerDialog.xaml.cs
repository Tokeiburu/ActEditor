using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using ActEditor.ApplicationConfiguration;
using ActEditor.Core.Avalon;
using ErrorManager;
using GRF.FileFormats.LubFormat;
using GRF.IO;
using GRF.GrfSystem;
using GrfToWpfBridge.Application;
using TokeiLibrary;
using TokeiLibrary.Paths;
using TokeiLibrary.WPF.Styles;
using TokeiLibrary.WPF.Styles.ListView;
using Utilities;
using Microsoft.CodeAnalysis;
using GRF.Threading;

namespace ActEditor.Core.WPF.Dialogs {
	/// <summary>
	/// Interaction logic for ScriptRunnerDialog.xaml
	/// </summary>
	public partial class ScriptRunnerDialog : TkWindow {
		public static string ScriptTemplate;

		public static string TmpFilePattern = "script_runner_{0:0000}.cs";
		private WpfRecentFiles _rcm;

		static ScriptRunnerDialog() {
			TmpFilePattern = Process.GetCurrentProcess().Id + "_" + TmpFilePattern;
			ScriptTemplate = Encoding.Default.GetString(ApplicationManager.GetResource("script_engine_template.txt"));
		}

		protected override void GRFEditorWindowKeyDown(object sender, KeyEventArgs e) {
			base.GRFEditorWindowKeyDown(sender, e);

			if (e.Key == Key.Escape) {
				Close();
			}
		}

		public ScriptRunnerDialog() : base("Script Runner", "dos.png", SizeToContent.WidthAndHeight, ResizeMode.CanResize) {
			InitializeComponent();

			ShowInTaskbar = true;
			WindowStartupLocation = WindowStartupLocation.CenterOwner;

			AvalonLoader.Load(_textEditor);
			AvalonLoader.SetSyntax(_textEditor, "C#");
			SizeToContent = SizeToContent.WidthAndHeight;
			_autoCompletion = new ScriptAutoCompletion(this);

			ListViewDataTemplateHelper.GenerateListViewTemplateNew(_listView, new ListViewDataTemplateHelper.GeneralColumnInfo[] {
				new ListViewDataTemplateHelper.ImageColumnInfo {Header = "", DisplayExpression = "DataImage", SearchGetAccessor = "IsWarning", FixedWidth = 20, MaxHeight = 24},
				new ListViewDataTemplateHelper.GeneralColumnInfo {Header = "Description", DisplayExpression = "Description", ToolTipBinding = "ToolTipDescription", TextAlignment = TextAlignment.Left, TextWrapping = TextWrapping.Wrap, IsFill = true},
				new ListViewDataTemplateHelper.GeneralColumnInfo {Header = "Line", DisplayExpression = "Line", FixedWidth = 50, ToolTipBinding = "Line", TextAlignment = TextAlignment.Right},
				new ListViewDataTemplateHelper.GeneralColumnInfo {Header = "Col", DisplayExpression = "Column", FixedWidth = 30, ToolTipBinding = "Column", TextAlignment = TextAlignment.Right},
			}, new DefaultListViewComparer<CompilerErrorView>(), new string[] { "Default", "{DynamicResource TextForeground}" });

			Loaded += delegate {
				SizeToContent = SizeToContent.Manual;
				MinWidth = 600;
				Width = MinWidth;
				MinHeight = _textEditor.MinHeight + _sp.ActualHeight + _gridActionPresenter.ActualHeight + 70;
				Height = MinHeight;

				Left = (SystemParameters.FullPrimaryScreenWidth - MinWidth) / 2d;
				Top = (SystemParameters.FullPrimaryScreenHeight - MinHeight) / 2d;
			};

			_textEditor.Text = ActEditorConfiguration.ActEditorScriptRunnerScript;
			_textEditor.TextChanged += delegate { ActEditorConfiguration.ActEditorScriptRunnerScript = _textEditor.Text; };
			_textEditor.PreviewKeyDown += new KeyEventHandler(_textEditor_PreviewKeyDown);

			_rcm = new WpfRecentFiles(ActEditorConfiguration.ConfigAsker, 6, _miLoadRecent, "Act Editor - ScriptRunner recent files");
			_rcm.FileClicked += new RecentFilesManager.RFMFileClickedEventHandler(_rcm_FileClicked);

			Closed += delegate {
				Owner = ActEditorWindow.Instance;
				ActEditorWindow.Instance.Focus();
				GC.Collect();
			};

			_textEditor.TextArea.TextEntered += (s, e) => {
				_autoCompletion.ProcessText(e);
			};


			// Force load Roslyn on startup
			GrfThread.Start(delegate {
				try {
					ScriptLoader.DummyCompile();
				}
				catch { }
			});
		}

		private void _textEditor_PreviewKeyDown(object sender, KeyEventArgs e) {
			var layer = AdornerLayer.GetAdornerLayer(_textEditor.TextArea);

			if (layer != null) {
				var adorners = layer.GetAdorners(_textEditor.TextArea);

				if (adorners != null && adorners.Length > 0)
					return;
			}

			switch (e.Key) {
				case Key.Back:
				case Key.Delete:
					_autoCompletion.UpdateFilter();
					break;
			}

			if (e.Key == Key.Escape) {
				_autoCompletion.CloseWindow();
			}
		}

		private ScriptAutoCompletion _autoCompletion;
		public const string DefaultScriptName = "\"MyCustomScript\"";
		public const string DefaultInputGesture = "null";
		public const string DefaultImage = "null";

		private void _buttonCancel_Click(object sender, RoutedEventArgs e) {
			Close();
		}

		private void _buttonRun_Click(object sender, RoutedEventArgs e) {
			try {
				string tmp = TemporaryFilesManager.GetTemporaryFilePath(TmpFilePattern);

				string script = _fixIndent(_textEditor.Text);
				script = ScriptTemplate.Replace("{0}", DefaultScriptName).Replace("{1}", DefaultInputGesture).Replace("{2}", DefaultImage).Replace("{3}", script);
				File.WriteAllText(tmp, script);

				string outDll;
				var res = ScriptLoader.Compile(tmp, out outDll);
				GrfPath.Delete(tmp);

				_listView.ItemsSource = null;

				if (!res.Success) {
					_listView.ItemsSource = res.Diagnostics.ToList().Select(p => new CompilerErrorView(p)).ToList();
					_sp.Visibility = Visibility.Visible;
				}
				else {
					var actScript = ScriptLoader.GetScriptObjectFromAssembly(outDll);

					try {
						var tab = ActEditorWindow.Instance.GetCurrentTab2();

						if (actScript.CanExecute(tab.Act, tab.SelectedAction, tab.SelectedFrame, tab.SelectionEngine.CurrentlySelected.ToArray())) {
							actScript.Execute(tab.Act, tab.SelectedAction, tab.SelectedFrame, tab.SelectionEngine.CurrentlySelected.ToArray());
						}
					}
					catch (Exception err) {
						ErrorHandler.HandleException(err);
					}
					finally {
						GC.Collect();
					}
				}
			}
			catch (Exception err) {
				ErrorHandler.HandleException(err);
			}
		}

		private string _fixIndent(string text) {
			if (text.Contains("act.Commands."))
				throw new Exception("Command methods cannot be executed within another command (Backup).");

			List<string> lines = text.Split(new string[] {"\r\n"}, StringSplitOptions.None).ToList();
			LineHelper.FixIndent(lines, 5);
			return string.Join("\r\n", lines.ToArray());
		}

		private void _listView_MouseDoubleClick(object sender, MouseButtonEventArgs e) {
			var obj = _listView.GetObjectAtPoint<ListViewItem>(e.GetPosition(_listView));

			if (obj != null) {
				var content = obj.Content as CompilerErrorView;

				if (content != null) {
					try {
						List<string> lines = _textEditor.Text.Split(new string[] {"\r\n"}, StringSplitOptions.None).ToList();

						int offset = 0;

						for (int i = 0; i < content.Line - 1; i++) {
							offset += lines[i].Length + 2;
						}

						//_textEditor.SelectionStart = offset + content.Column;
						_textEditor.SelectionLength = 0;
						//_textEditor.CaretOffset = offset + content.Column;
						_textEditor.TextArea.Caret.Offset = offset + content.Column;
						_textEditor.TextArea.Caret.BringCaretToView();
						_textEditor.TextArea.Caret.Show();
						Keyboard.Focus(_textEditor);
					}
					catch {
					}
				}
			}
		}

		public string TabsToSpace() {
			var textWithTabs = _textEditor.Text;
			StringBuilder builder = new StringBuilder();

			var textValues = textWithTabs.Split('\t');

			foreach (var val in textValues) {
				builder.Append(val);
				builder.Append(new string(' ', 4 - val.Length % 4));
			}

			return builder.ToString();
		}

		private void _buttonClear_Click(object sender, RoutedEventArgs e) {
			_listView.ItemsSource = null;
			_textEditor.Text = "";
		}

		private void _buttonSaveAs_Click(object sender, RoutedEventArgs e) {
			try {
				string path = TkPathRequest.SaveFile<ActEditorConfiguration>("AppLastPath", "filter", "C# Files|*.cs");

				if (path != null) {
					string script = _fixIndent(_textEditor.Text);
					script = ScriptTemplate.Replace("{0}", DefaultScriptName).Replace("{1}", DefaultInputGesture).Replace("{2}", DefaultImage).Replace("{3}", script);
					File.WriteAllText(path, script);
					_rcm.AddRecentFile(path);
				}
			}
			catch (Exception err) {
				ErrorHandler.HandleException(err);
			}
		}

		private void _buttonLoad_Click(object sender, RoutedEventArgs e) {
			try {
				string path = TkPathRequest.OpenFile<ActEditorConfiguration>("AppLastPath", "filter", "C# Files|*.cs");

				if (path != null) {
					_textEditor.Text = _loadScriptFromFile(File.ReadAllText(path));
					_rcm.AddRecentFile(path);
				}
			}
			catch (Exception err) {
				ErrorHandler.HandleException(err);
			}
		}

		private string _extractBackup(string input) {
			try {
				string text = input;

				const string ToFindBackup = "act.Commands.Backup(_ => {";

				int first = text.IndexOf(ToFindBackup, StringComparison.Ordinal);

				if (first < 0) {
					return _extractBackupNoBackup(input);
				}
				
				text = text.Substring(first + ToFindBackup.Length);

				int end = text.IndexOf("act.Commands.CancelEdit();", StringComparison.Ordinal);

				if (end < 0) {
					return _extractBackupNoBackup(input);
				}

				int count = 0;


				while (end >= 0) {
					if (text[end] == '}')
						count++;

					end--;

					if (count == 2) {
						break;
					}
				}

				if (end < 0)
					return input;

				text = text.Substring(0, end - 1).Trim('\r', '\n');

				List<string> lines = text.Split(new string[] {"\r\n"}, StringSplitOptions.None).ToList();
				LineHelper.FixIndent(lines, 0);

				var backupErrIndex = text.IndexOf("backupErr = err;", StringComparison.Ordinal);
				if (backupErrIndex > -1) {
					lines.RemoveAt(0);

					for (int i = 0; i < 9; i++) {
						if (lines.Count < 1)
							break;

						lines.RemoveAt(lines.Count - 1);
					}
				}

				LineHelper.FixIndent(lines, 0);

				return string.Join("\r\n", lines.ToArray()).TrimEnd('\r', '\n', ' ', '\t');
			}
			catch {
				return input;
			}
		}

		private string _extractBackupNoBackup(string input) {
			try {
				string text = input;

				const string ToFindExecute = "\t\tpublic void Execute(Act act, int selectedActionIndex, int selectedFrameIndex, int[] selectedLayerIndexes) {";

				int first = text.IndexOf(ToFindExecute, StringComparison.Ordinal);

				if (first < 0) {
					return input;
				}

				text = text.Substring(first + ToFindExecute.Length);

				int end = text.IndexOf("\n\t\t}", StringComparison.Ordinal);

				if (end < 0)
					end = text.IndexOf("\r\t\t}", StringComparison.Ordinal);

				if (end < 0) {
					return input;
				}

				text = text.Substring(0, end - 1).Trim('\r', '\n');

				List<string> lines = text.Split(new string[] { "\r\n" }, StringSplitOptions.None).ToList();
				LineHelper.FixIndent(lines, 0);
				return string.Join("\r\n", lines.ToArray()).TrimEnd('\r', '\n', ' ', '\t');
			}
			catch {
				return input;
			}
		}

		private void _buttonHelp_Click(object sender, RoutedEventArgs e) {
			string indexPath = GrfPath.Combine(Configuration.ApplicationPath, "doc", "index.html");

			if (File.Exists(indexPath)) {
				Process.Start(indexPath);
			}
			else {
				ErrorHandler.HandleException("Path not found: " + indexPath);
			}
		}

		private string _loadScriptFromFile(string text) {
			return _extractBackup(text);
		}

		private void _rcm_FileClicked(string file) {
			try {
				_textEditor.Document.Replace(0, _textEditor.Document.TextLength, _loadScriptFromFile(File.ReadAllText(file)));
			}
			catch (Exception err) {
				_rcm.RemoveRecentFile(file);
				ErrorHandler.HandleException(err);
			}
		}

		#region Nested type: CompilerErrorView

		public class CompilerErrorView {
			public CompilerErrorView(Diagnostic error) {
				Description = error.GetMessage();
				ToolTipDescription = error.ToString();
				var lineSpan = error.Location.GetLineSpan();
				Line = lineSpan.StartLinePosition.Line - 63 + 1;
				Column = lineSpan.StartLinePosition.Character - 6 + 1;

				IsWarning = (int)error.Severity;

				switch (error.Severity) {
					case DiagnosticSeverity.Error:
						DataImage = ApplicationManager.PreloadResourceImage("error16.png");
						break;
					case DiagnosticSeverity.Warning:
						DataImage = ApplicationManager.PreloadResourceImage("warning16.png");
						break;
					case DiagnosticSeverity.Info:
						DataImage = ApplicationManager.PreloadResourceImage("help.png");
						break;
					case DiagnosticSeverity.Hidden:
						DataImage = ApplicationManager.PreloadResourceImage("settings.png");
						break;
				}
			}

			public CompilerErrorView(CompilerError error) {
				Description = error.ErrorText;
				ToolTipDescription = error.ToString();
				Line = error.Line - 63;
				Column = error.Column - 6;

				IsWarning = error.IsWarning ? 0 : 1;
				DataImage = error.IsWarning ? ApplicationManager.PreloadResourceImage("warning16.png") : ApplicationManager.PreloadResourceImage("error16.png");
			}

			public string Description { get; set; }
			public string ToolTipDescription { get; set; }
			public int Line { get; set; }
			public int Column { get; set; }
			public object DataImage { get; set; }
			public int IsWarning { get; set; }

			public bool Default {
				get { return true; }
			}
		}

		#endregion
	}
}