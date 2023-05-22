using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ActEditor.ApplicationConfiguration;
using ActEditor.Core.WPF.GenericControls;
using ErrorManager;
using GRF.Image;
using GrfToWpfBridge;
using GrfToWpfBridge.MultiGrf;
using TokeiLibrary;
using TokeiLibrary.Shortcuts;
using TokeiLibrary.WPF;
using TokeiLibrary.WPF.Styles;
using Utilities;
using Utilities.Extension;
using Utilities.Services;
using Binder = GrfToWpfBridge.Binder;

namespace ActEditor.Core.WPF.Dialogs {
	/// <summary>
	/// Interaction logic for ActEditorSettings.xaml
	/// </summary>
	public partial class StyleEditor : TkWindow {
		private ResourceDictionary _dico;

		public StyleEditor()
			: base("Settings", "settings.png") {
			InitializeComponent();

			var path = "pack://application:,,,/" + Assembly.GetEntryAssembly().GetName().Name.Replace(" ", "%20") + ";component/WPF/Styles/";
			path += "StyleDark.xaml";

			var dico = new ResourceDictionary { Source = new Uri(path, UriKind.RelativeOrAbsolute) };
			_dico = dico;

			int row = 0;

			foreach (var key in dico.Keys.OfType<string>().OrderBy(p => p)) {
				var value = dico[key];

				if (value is SolidColorBrush) {
					_gridColors.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(0, GridUnitType.Auto) });

					Label label = new Label();
					SolidColorBrush brush = (SolidColorBrush)value;

					try {
						label.Content = key;
						label.SetValue(Grid.RowProperty, row);
						_gridColors.Children.Add(label);

						QuickColorSelector qcs = new QuickColorSelector();
						qcs.SetValue(Grid.RowProperty, row);
						qcs.SetValue(Grid.ColumnProperty, 1);
						GrfColor oriColor = brush.Color.ToGrfColor();
						GrfColor current = brush.Color.ToGrfColor();
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
			//qcs.Init(ActEditorConfiguration.ConfigAsker.RetrieveSetting(() => get()));
			qcs.PreviewUpdateInterval = 500;

			qcs.ColorChanged += delegate(object sender, Color value) {
				set(value.ToGrfColor());
				dico[name] = new SolidColorBrush() { Color = value };
				_reload();
			};

			qcs.PreviewColorChanged += delegate(object sender, Color value) {
				set(value.ToGrfColor());
				dico[name] = new SolidColorBrush() { Color = value };
				_reload();
			};
		}

		private void _reload() {
			Application.Current.Resources.MergedDictionaries.RemoveAt(Application.Current.Resources.MergedDictionaries.Count - 1);
			Application.Current.Resources.MergedDictionaries.Add(_dico);
		}

		private void _buttonOk_Click(object sender, RoutedEventArgs e) {
			_reload();
		}
	}
}