using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ActEditor.ApplicationConfiguration;
using ActEditor.Core.Avalon;
using ErrorManager;
using GRF.FileFormats.LubFormat;
using GRF.IO;
using GRF.System;
using GrfToWpfBridge.Application;
using ICSharpCode.AvalonEdit.Highlighting;
using TokeiLibrary;
using TokeiLibrary.WPF.Styles;
using TokeiLibrary.WPF.Styles.ListView;
using Utilities;

namespace ActEditor.Core.WPF.Dialogs {
	/// <summary>
	/// Interaction logic for ScriptRunnerDialog.xaml
	/// </summary>
	public partial class ScriptRunnerDialog : TkWindow {
		public const string ScriptTemplate = "using System;\r\n" +
		                                     "using System.Collections.Generic;\r\n" +
		                                     "using System.Globalization;\r\n" +
		                                     "using System.IO;\r\n" +
		                                     "using System.Linq;\r\n" +
		                                     "using System.Windows;\r\n" +
		                                     "using System.Windows.Controls;\r\n" +
		                                     "using System.Windows.Documents;\r\n" +
		                                     "using System.Windows.Media;\r\n" +
		                                     "using System.Windows.Media.Imaging;\r\n" +
		                                     "using ErrorManager;\r\n" +
		                                     "using GRF.FileFormats.ActFormat;\r\n" +
		                                     "using GRF.FileFormats.SprFormat;\r\n" +
		                                     "using GRF.FileFormats.PalFormat;\r\n" +
		                                     "using GRF.Image;\r\n" +
		                                     "using GRF.Image.Decoders;\r\n" +
											 "using GRF.Graphics;\r\n" +
											 "using GRF.Core;\r\n" +
											 "using GRF.IO;\r\n" +
											 "using GRF.System;\r\n" +
		                                     "using GrfToWpfBridge;\r\n" +
		                                     "using TokeiLibrary;\r\n" +
											 "using TokeiLibrary.WPF;\r\n" +
											 "using Utilities;\r\n" +
											 "using Utilities.Extension;\r\n" +
		                                     "using Action = GRF.FileFormats.ActFormat.Action;\r\n" +
		                                     "using Frame = GRF.FileFormats.ActFormat.Frame;\r\n" +
		                                     "using Point = System.Windows.Point;\r\n" +
		                                     "\r\n" +
		                                     "namespace Scripts {\r\n" +
		                                     "    public class Script : IActScript {\r\n" +
		                                     "		public object DisplayName {\r\n" +
		                                     "			get { return {0}; }\r\n" +
		                                     "		}\r\n" +
		                                     "		\r\n" +
		                                     "		public string Group {\r\n" +
		                                     "			get { return \"Scripts\"; }\r\n" +
		                                     "		}\r\n" +
		                                     "		\r\n" +
		                                     "		public string InputGesture {\r\n" +
		                                     "			get { return {1}; }\r\n" +
		                                     "		}\r\n" +
		                                     "		\r\n" +
		                                     "		public string Image {\r\n" +
		                                     "			get { return {2}; }\r\n" +
		                                     "		}\r\n" +
		                                     "		\r\n" +
		                                     "		public void Execute(Act act, int selectedActionIndex, int selectedFrameIndex, int[] selectedLayerIndexes) {\r\n" +
		                                     "			if (act == null) return;\r\n" +
											 "			\r\n" +
											 "			Exception backupErr = null;\r\n" +
											 "			\r\n" +
		                                     "			try {\r\n" +
											 "				act.Commands.BeginNoDelay();\r\n" +
											 "				act.Commands.Backup(_ => {\r\n" +
											 "					try {\r\n" +
		                                     "{3}\r\n" +
											 "					}\r\n" +
											 "					catch (Exception err) {\r\n" +
											 "						backupErr = err;\r\n" +
											 "					}\r\n" +
		                                     "				}, {0}, true);\r\n" +
											 "				\r\n" +
											 "				if (backupErr != null) {\r\n" +
											 "					throw backupErr;\r\n" +
											 "				}\r\n" +
		                                     "			}\r\n" +
		                                     "			catch (Exception err) {\r\n" +
		                                     "				act.Commands.CancelEdit();\r\n" +
		                                     "				ErrorHandler.HandleException(err, ErrorLevel.Warning);\r\n" +
		                                     "			}\r\n" +
		                                     "			finally {\r\n" +
		                                     "				act.Commands.End();\r\n" +
		                                     "				act.InvalidateVisual();\r\n" +
		                                     "				act.InvalidateSpriteVisual();\r\n" +
		                                     "			}\r\n" +
		                                     "		}\r\n" +
		                                     "		\r\n" +
		                                     "		public bool CanExecute(Act act, int selectedActionIndex, int selectedFrameIndex, int[] selectedLayerIndexes) {\r\n" +
		                                     "			return true;\r\n" +
											 "			//return act != null;\r\n" +
		                                     "		}\r\n" +
		                                     "	}\r\n" +
		                                     "}\r\n";

		public static string TmpFilePattern = "script_runner_{0:0000}.cs";
		private WpfRecentFiles _rcm;

		static ScriptRunnerDialog() {
			TmpFilePattern = Process.GetCurrentProcess().Id + "_" + TmpFilePattern;
			TemporaryFilesManager.UniquePattern(TmpFilePattern);
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

			ListViewDataTemplateHelper.GenerateListViewTemplateNew(_listView, new ListViewDataTemplateHelper.GeneralColumnInfo[] {
				new ListViewDataTemplateHelper.ImageColumnInfo {Header = "", DisplayExpression = "DataImage", SearchGetAccessor = "IsWarning", FixedWidth = 20, MaxHeight = 24},
				new ListViewDataTemplateHelper.GeneralColumnInfo {Header = "Description", DisplayExpression = "Description", ToolTipBinding = "ToolTipDescription", TextAlignment = TextAlignment.Left, TextWrapping = TextWrapping.Wrap, IsFill = true},
				new ListViewDataTemplateHelper.GeneralColumnInfo {Header = "Line", DisplayExpression = "Line", FixedWidth = 50, ToolTipBinding = "Line", TextAlignment = TextAlignment.Right},
				new ListViewDataTemplateHelper.GeneralColumnInfo {Header = "Col", DisplayExpression = "Column", FixedWidth = 30, ToolTipBinding = "Column", TextAlignment = TextAlignment.Right},
			}, new DefaultListViewComparer<CompilerErrorView>(), new string[] { "Default", "{DynamicResource TextForeground}" });

			Loaded += delegate {
				SizeToContent = SizeToContent.Manual;
				//_listView.MaxWidth = _mainGrid.ActualWidth;
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

			//Binder.Bind(_miAutocomplete, () => GrfEditorConfiguration.ActEditorScriptRunnerAutocomplete);

			Closed += delegate {
				Owner = ActEditorWindow.Instance;
				ActEditorWindow.Instance.Focus();
			};
		}

		private void _textEditor_PreviewKeyDown(object sender, KeyEventArgs e) {
			if (e.Key == Key.Escape) {
				Close();
				e.Handled = true;
			}
		}

		public string ScriptName {
			get { return "\"MyCustomScript\""; }
		}

		public string InputGesture {
			get { return "null"; }
		}

		public string Image {
			get { return "null"; }
		}

		private void _buttonCancel_Click(object sender, RoutedEventArgs e) {
			Close();
		}

		private void _buttonRun_Click(object sender, RoutedEventArgs e) {
			try {
				string tmp = TemporaryFilesManager.GetTemporaryFilePath(TmpFilePattern);

				string script = _fixIndent(_textEditor.Text);
				script = ScriptTemplate.Replace("{0}", ScriptName).Replace("{1}", InputGesture).Replace("{2}", Image).Replace("{3}", script);
				File.WriteAllText(tmp, script);

				string outDll;
				var res = ScriptLoader.Compile(tmp, out outDll);
				GrfPath.Delete(tmp);

				_listView.ItemsSource = null;

				if (res.Errors.Count > 0) {
					_listView.ItemsSource = res.Errors.Cast<CompilerError>().ToList().Select(p => new CompilerErrorView(p)).ToList();
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
				string path = PathRequest.SaveFileEditor("filter", "C# Files|*.cs");

				if (path != null) {
					string script = _fixIndent(_textEditor.Text);
					script = ScriptTemplate.Replace("{0}", ScriptName).Replace("{1}", InputGesture).Replace("{2}", Image).Replace("{3}", script);
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
				string path = PathRequest.OpenFileEditor("filter", "C# Files|*.cs");

				if (path != null) {
					string text = File.ReadAllText(path);
					text = _extractBackup(text);
					_textEditor.Text = text;
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
					return input;
				}

				text = text.Substring(first + ToFindBackup.Length);

				int end = text.IndexOf("act.Commands.CancelEdit();", StringComparison.Ordinal);

				if (end < 0) {
					return input;
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
				ErrorHandler.HandleException("Path not found : " + indexPath);
			}
		}

		private void _rcm_FileClicked(string file) {
			try {
				_textEditor.Document.Replace(0, _textEditor.Document.TextLength, File.ReadAllText(file));
			}
			catch (Exception err) {
				_rcm.RemoveRecentFile(file);
				ErrorHandler.HandleException(err);
			}
		}

		#region Nested type: CompilerErrorView

		public class CompilerErrorView {
			public CompilerErrorView(CompilerError error) {
				Description = error.ErrorText;
				ToolTipDescription = error.ToString();

				Line = error.Line - 56;
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