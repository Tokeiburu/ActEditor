using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using GRF.FileFormats.ActFormat;
using TokeiLibrary;
using TokeiLibrary.WPF.Styles;
using TokeiLibrary.WPF.Styles.ListView;

namespace ActEditor.Core.WPF.Dialogs {
	/// <summary>
	/// Interaction logic for SoundEditDialog.xaml
	/// </summary>
	public partial class UsageDialog : TkWindow {
		private readonly IFrameRendererEditor _editor;

		public UsageDialog() {
			InitializeComponent();
		}

		public UsageDialog(IFrameRendererEditor editor, IEnumerable<ActIndex> indexes) : base("Usage", "help.ico") {
			_editor = editor;
			InitializeComponent();
			
			Owner = WpfUtilities.TopWindow;

			_listView.ItemsSource = indexes;

			ListViewDataTemplateHelper.GenerateListViewTemplateNew(_listView, new ListViewDataTemplateHelper.GeneralColumnInfo[] {
				new ListViewDataTemplateHelper.GeneralColumnInfo {Header = "Action", DisplayExpression = "ActionIndex", SearchGetAccessor = "ActionIndex", TextAlignment = TextAlignment.Right, ToolTipBinding = "ActionIndex", FixedWidth = 70},
				new ListViewDataTemplateHelper.GeneralColumnInfo {Header = "Frame", DisplayExpression = "FrameIndex", SearchGetAccessor = "FrameIndex", TextAlignment = TextAlignment.Right, ToolTipBinding = "FrameIndex", FixedWidth = 70},
				new ListViewDataTemplateHelper.GeneralColumnInfo {Header = "Layer", DisplayExpression = "LayerIndex", SearchGetAccessor = "LayerIndex", TextAlignment = TextAlignment.Right, ToolTipBinding = "LayerIndex", FixedWidth = 70},
			}, new DefaultListViewComparer<ActIndex>(), new string[] {"Default", "Black"});

			_listView.SelectionChanged += new SelectionChangedEventHandler(_listView_SelectionChanged);
		}

		private void _listView_SelectionChanged(object sender, SelectionChangedEventArgs e) {
			var element = _listView.SelectedItem as ActIndex;

			if (element != null) {
				_editor.FrameSelector.SetAction(element.ActionIndex);
				_editor.FrameSelector.SetFrame(element.FrameIndex);
				_editor.SelectionEngine.DeselectAll();
				_editor.SelectionEngine.Select(element.LayerIndex);
			}
		}

		protected override void GRFEditorWindowKeyDown(object sender, KeyEventArgs e) {
			if (e.Key == Key.Escape || e.Key == Key.Enter)
				Close();
		}

		private void _buttonOk_Click(object sender, RoutedEventArgs e) {
			Close();
		}

		private void _buttonCancel_Click(object sender, RoutedEventArgs e) {
			Close();
		}
	}
}