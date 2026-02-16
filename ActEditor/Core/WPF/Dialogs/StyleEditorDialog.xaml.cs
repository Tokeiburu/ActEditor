using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ActEditor.ApplicationConfiguration;
using ActEditor.Core.WPF.GenericControls;
using ErrorManager;
using GRF.Image;
using GRF.IO;
using GrfToWpfBridge;
using TokeiLibrary.WPF.Styles;
using Utilities;

namespace ActEditor.Core.WPF.Dialogs {
	/// <summary>
	/// Interaction logic for ActEditorSettings.xaml
	/// </summary>
	public partial class StyleEditor : TkWindow {
		private ResourceDictionary _dico;

		private Dictionary<string, Color> _newColors = new Dictionary<string, Color>();

		public StyleEditor()
			: base("Settings", "settings.png") {
			InitializeComponent();

			if (ActEditorConfiguration.StyleTheme == "" || ActEditorConfiguration.StyleTheme == "Dark theme")
				throw new Exception("Cannot edit default themes. Change to a custom theme first.");

			var path = "pack://application:,,,/" + Assembly.GetEntryAssembly().GetName().Name.Replace(" ", "%20") + ";component/WPF/Styles/StyleDark.xaml";

			var dico = new ResourceDictionary { Source = new Uri(path, UriKind.RelativeOrAbsolute) };
			var dicoNew = new ResourceDictionary { Source = new Uri(GrfPath.Combine(ActEditorConfiguration.ProgramDataPath, "Themes", ActEditorConfiguration.StyleTheme + ".xaml"), UriKind.RelativeOrAbsolute) };
			_dico = dico;

			int row = 0;

			foreach (var key in dico.Keys.OfType<string>().OrderBy(p => p)) {
				var value = dico[key];

				if (key.Contains("UIThemeTreeView") || key.Contains("UIThemeProperty"))
					continue;

				if (value is Color) {
					_gridColors.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(0, GridUnitType.Auto) });

					Label label = new Label();
					Color brush = (Color)value;

					try {
						label.Content = key;
						label.SetValue(Grid.RowProperty, row);
						_gridColors.Children.Add(label);

						QuickColorSelector qcs = new QuickColorSelector();
						qcs.SetValue(Grid.RowProperty, row);
						qcs.SetValue(Grid.ColumnProperty, 1);
						GrfColor oriColor = brush.ToGrfColor();
						GrfColor current = brush.ToGrfColor();

						if (dicoNew.Contains(key)) {
							current = ((Color)dicoNew[key]).ToGrfColor();
						}

						_gridColors.Children.Add(qcs);

						_set(qcs, () => oriColor, () => current, v => current = v, key, dico);
						row++;
					}
					catch (Exception err) {
						ErrorHandler.HandleException(err);
					}
					Z.F();
				}
			}
		}

		private void _set(QuickColorSelector qcs, Func<GrfColor> def, Func<GrfColor> get, Action<GrfColor> set, string name, ResourceDictionary dico) {
			qcs.Color = get().ToColor();
			qcs.Init(() => def());
			qcs.PreviewUpdateInterval = 500;

			qcs.ColorChanged += delegate(object sender, Color value) {
				set(value.ToGrfColor());
				dico[name] = value;
				_newColors[name] = value;
				_reload();
			};

			qcs.PreviewColorChanged += delegate(object sender, Color value) {
				set(value.ToGrfColor());
				dico[name] = value;
				_newColors[name] = value;
				_reload();
			};
		}

		private void _reload() {
			while (Application.Current.Resources.MergedDictionaries.Count > 2) {
				Application.Current.Resources.MergedDictionaries.RemoveAt(2);
			}

			var path = "pack://application:,,,/" + Assembly.GetEntryAssembly().GetName().Name.Replace(" ", "%20") + ";component/WPF/Styles/StyleDark.xaml";
			Application.Current.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri(path, UriKind.RelativeOrAbsolute) });
			path = GrfPath.Combine(ActEditorConfiguration.ProgramDataPath, "Themes", ActEditorConfiguration.StyleTheme + ".xaml");

			StringBuilder b = new StringBuilder();
			b.AppendLine("<ResourceDictionary xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\"" + "\r\n" + 
                    "\txmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\"" + "\r\n" + 
                    "\txmlns:WPF=\"clr-namespace:TokeiLibrary.WPF;assembly=TokeiLibrary\"" + "\r\n" + 
                    "\txmlns:TreeViewManager=\"clr-namespace:GrfToWpfBridge.TreeViewManager;assembly=GrfToWpfBridge\"" + "\r\n" + 
                    "\txmlns:styles=\"clr-namespace:TokeiLibrary.WPF.Styles;assembly=TokeiLibrary\"" + "\r\n" + 
                    "\txmlns:wpfBugFix=\"clr-namespace:TokeiLibrary.WpfBugFix;assembly=TokeiLibrary\"" + "\r\n" + 
                    "\txmlns:themes=\"clr-namespace:Microsoft.Windows.Themes;assembly=PresentationFramework.Aero\"" + "\r\n" + 
                    "\txmlns:application=\"clr-namespace:GrfToWpfBridge.Application;assembly=GrfToWpfBridge\"" + "\r\n" + 
                    "\txmlns:genericControls=\"clr-namespace:ActEditor.Core.WPF.GenericControls\"" + "\r\n" + 
                    "\txmlns:colorTextBox=\"clr-namespace:ColorPicker.ColorTextBox;assembly=ColorPicker\">");
			foreach (var key in _dico.Keys.OfType<string>().OrderBy(p => p)) {
				var value = _dico[key];

				if (key.Contains("UIThemeTreeView") || key.Contains("UIThemeProperty"))
					continue;

				if (value is Color) {
					var color = (Color)value;

					if (_newColors.ContainsKey(key))
						color = _newColors[key];

					b.AppendLine("\t<Color x:Key=\"" + key + "\">" + color.ToGrfColor().ToHexString() + "</Color>");
				}
			}
			b.AppendLine("</ResourceDictionary>");

			File.WriteAllText(path, b.ToString());

			Application.Current.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri(path, UriKind.RelativeOrAbsolute) });
		}

		private void _buttonOk_Click(object sender, RoutedEventArgs e) {
			Close();
		}
	}
}