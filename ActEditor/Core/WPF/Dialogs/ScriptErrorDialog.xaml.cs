using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using ActEditor.Core.Avalon;
using ErrorManager;
using TokeiLibrary;
using TokeiLibrary.WPF.Styles;
using TokeiLibrary.WPF.Styles.ListView;
using Microsoft.CodeAnalysis;
using static ActEditor.Core.WPF.Dialogs.ScriptRunnerDialog;
using Utilities.Services;
using TokeiLibrary.Shortcuts;
using ActEditor.Core.Scripting;

namespace ActEditor.Core.WPF.Dialogs {
	/// <summary>
	/// Interaction logic for ScriptRunnerDialog.xaml
	/// </summary>
	public partial class ScriptErrorDialog : TkWindow {
		private ScriptAutoCompletion _autoCompletion;

		public ScriptErrorDialog() : base("Script errors", "error16.png", SizeToContent.WidthAndHeight, ResizeMode.CanResize) {
			InitializeComponent();
		}

		public ScriptErrorDialog(ScriptLoaderResult scriptLoaderResult) : base("Script errors", "dos.png", SizeToContent.WidthAndHeight, ResizeMode.CanResize) {
			InitializeComponent();

			ShowInTaskbar = true;
			WindowStartupLocation = WindowStartupLocation.CenterScreen;
			SizeToContent = SizeToContent.WidthAndHeight;

			AvalonLoader.Load(_textEditor);
			AvalonLoader.SetSyntax(_textEditor, "C#");
			_autoCompletion = new ScriptAutoCompletion(_textEditor);

			ListViewDataTemplateHelper.GenerateListViewTemplateNew(_listView, new ListViewDataTemplateHelper.GeneralColumnInfo[] {
				new ListViewDataTemplateHelper.ImageColumnInfo {Header = "", DisplayExpression = "DataImage", SearchGetAccessor = "IsWarning", FixedWidth = 20, MaxHeight = 24},
				new ListViewDataTemplateHelper.GeneralColumnInfo {Header = "Description", DisplayExpression = "Description", ToolTipBinding = "ToolTipDescription", TextAlignment = TextAlignment.Left, TextWrapping = TextWrapping.Wrap, IsFill = true},
				new ListViewDataTemplateHelper.GeneralColumnInfo {Header = "Line", DisplayExpression = "Line", FixedWidth = 50, ToolTipBinding = "Line", TextAlignment = TextAlignment.Right},
				new ListViewDataTemplateHelper.GeneralColumnInfo {Header = "Col", DisplayExpression = "Column", FixedWidth = 30, ToolTipBinding = "Column", TextAlignment = TextAlignment.Right},
			}, new DefaultListViewComparer<CompilerErrorView>(), new string[] { "Default", "{DynamicResource TextForeground}" });

			ListViewDataTemplateHelper.GenerateListViewTemplateNew(_lvFiles, new ListViewDataTemplateHelper.GeneralColumnInfo[] {
				new ListViewDataTemplateHelper.GeneralColumnInfo {Header = "File name", DisplayExpression = "DisplayFileName", ToolTipBinding = "FileName", TextAlignment = TextAlignment.Left, TextWrapping = TextWrapping.Wrap, IsFill = true},
			}, new DefaultListViewComparer<ScriptFileErrorView>(), new string[] { "Default", "{DynamicResource TextForeground}" });

			ApplicationShortcut.Link(ApplicationShortcut.FromString("Ctrl-S", "ScriptError.Save"), Save, this);

			_lvFiles.ItemsSource = scriptLoaderResult.Errors.Select(p => new ScriptFileErrorView(p)).ToList();
			_textEditor.PreviewKeyDown += new KeyEventHandler(_textEditor_PreviewKeyDown);

			Loaded += delegate {
				SizeToContent = SizeToContent.Manual;
				MinWidth = 700;
				Width = MinWidth;
				MinHeight = _gridRowTextEditor.MinHeight + _sp.ActualHeight + _gridActionPresenter.ActualHeight + 70;
				Height = MinHeight;

				//SizeToContent = SizeToContent.Manual;
				_gridRowTextEditor.Height = new GridLength(1, GridUnitType.Star);

				Left = (Owner.ActualWidth - MinWidth) / 2d + Owner.Left;
				Top = (Owner.ActualHeight - MinHeight) / 2d + Owner.Top;

				_lvFiles.SelectedIndex = 0;
			};

			Closed += delegate {
				Owner?.Focus();
			};

			_textEditor.TextArea.TextEntered += (s, e) => {
				_autoCompletion.ProcessText(e);
			};

			this.PreviewKeyDown += _scriptErrorDialog_PreviewKeyDown;
		}

		private void _scriptErrorDialog_PreviewKeyDown(object sender, KeyEventArgs e) {
			if (e.Key == Key.Escape)
				Close();
		}

		public void Save() {
			try {
				var errorView = _lvFiles.SelectedItem as ScriptFileErrorView;

				if (errorView != null) {
					File.WriteAllText(errorView.FileName, _textEditor.Text);
				}
			}
			catch (Exception err) {
				ErrorHandler.HandleException(err);
			}
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

		private void _buttonCancel_Click(object sender, RoutedEventArgs e) {
			Close();
		}

		private void _listView_MouseDoubleClick(object sender, MouseButtonEventArgs e) {
			var obj = _listView.GetObjectAtPoint<ListViewItem>(e.GetPosition(_listView));

			if (obj != null) {
				FocusError(obj.Content as CompilerErrorView);
			}
		}

		public class ScriptFileErrorView {
			private LoadScriptResult _result;

			public ScriptFileErrorView(LoadScriptResult result) {
				_result = result;
				DisplayFileName = Path.GetFileName(result.OriginalScriptPath);
				FileName = result.OriginalScriptPath;
			}

			public string DisplayFileName { get; set; }
			public string FileName { get; set; }
			public LoadScriptResult LoadScriptResult => _result;

			public bool Default {
				get { return true; }
			}
		}

		private void _lvFiles_SelectionChanged(object sender, SelectionChangedEventArgs e) {
			SelectScriptFile(_lvFiles.SelectedItem as ScriptFileErrorView);
		}

		public void SelectScriptFile(ScriptFileErrorView scriptFileErrorView) {
			_textEditor.Text = "";

			if (scriptFileErrorView == null)
				return;

			try {
				_textEditor.Text = File.ReadAllText(scriptFileErrorView.LoadScriptResult.ScriptPath);
				_listView.ItemsSource = _listView.ItemsSource = scriptFileErrorView.LoadScriptResult.EmitResult.Diagnostics.ToList().Select(p => new CompilerErrorView(p, 0, -1)).ToList();
				FocusError(_listView.Items[0] as CompilerErrorView);
			}
			catch {

			}
		}

		private void _listView_SelectionChanged(object sender, SelectionChangedEventArgs e) {
			FocusError(_listView.SelectedItem as CompilerErrorView);
		}

		public void FocusError(CompilerErrorView compilerErrorView) {
			if (compilerErrorView == null)
				return;

			try {
				Dispatcher.BeginInvoke(new Action(() => {
					List<string> lines = _textEditor.Text.Split(new string[] { "\r\n" }, StringSplitOptions.None).ToList();

					int offset = 0;

					for (int i = 0; i < compilerErrorView.Line - 1; i++) {
						offset += lines[i].Length + 2;
					}

					_textEditor.SelectionLength = 0;
					_textEditor.TextArea.Caret.Offset = offset + compilerErrorView.Column;
					_textEditor.TextArea.Caret.BringCaretToView();
					_textEditor.TextArea.Caret.Show();

					Keyboard.Focus(_textEditor);
				}), System.Windows.Threading.DispatcherPriority.Render);
			}
			catch {
			}
		}

		private void _miSelect_Click(object sender, RoutedEventArgs e) {
			var errorView = _lvFiles.SelectedItem as ScriptFileErrorView;

			if (errorView != null) {
				try {
					OpeningService.FileOrFolder(errorView.FileName);
				}
				catch {
				}
			}
		}

		private void _miOpen_Click(object sender, RoutedEventArgs e) {
			var errorView = _lvFiles.SelectedItem as ScriptFileErrorView;

			if (errorView != null) {
				try {
					Process.Start(errorView.FileName);
				}
				catch {
				}
			}
		}

		public void UpdateErrors(ScriptLoaderResult scriptLoaderResult) {
			var errorView = _lvFiles.SelectedItem as ScriptFileErrorView;
			string previousFile = errorView != null ? errorView.FileName : "";

			_textEditor.Text = "";
			_lvFiles.ItemsSource = scriptLoaderResult.Errors.Select(p => new ScriptFileErrorView(p)).ToList();

			for (int i = 0; i < scriptLoaderResult.Errors.Count; i++) {
				if (scriptLoaderResult.Errors[i].OriginalScriptPath == previousFile) {
					_lvFiles.SelectedIndex = i;
					break;
				}
			}
		}
	}
}