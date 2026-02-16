using ActEditor.Core.WPF.Dialogs;
using GRF.FileFormats.ActFormat;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using TokeiLibrary;
using Utilities;

namespace ActEditor.Core.Scripts {
	[Flags]
	public enum AnimationEditTypes {
		None,
		Animation = 1 << 0,
		Layer = 1 << 1,
		Loop = 1 << 2,
		AddEmptyFrame = 1 << 3,
		Length = 1 << 4,
		Start = 1 << 5,
		Full = Animation | Layer | Loop | AddEmptyFrame | Length | Start,
		TargetOnly = Animation | Layer,
	};

	/// <summary>
	/// Interaction logic for UserControl1.xaml
	/// </summary>
	public partial class AnimationEditComponent : UserControl, ICustomPreviewProperty {
		private Act _act;

		public AnimationEditComponent() {
			InitializeComponent();
		}

		private List<ToggleButton> _toggleAnimButtons = new List<ToggleButton>();
		private List<ToggleButton> _toggleLayerButtons = new List<ToggleButton>();
		private ToggleButton _tbAnimAll;
		private ToggleButton _tbLayerAll;
		private System.Action _update;
		private EffectConfiguration.EffectProperty _effectProperty;
		private bool _eventsEnabled = true;
		private int _eventsEnabledCounter = 0;

		public class AnimationData {
			public HashSet<int> Animations = new HashSet<int>();
			public HashSet<int> Layers = new HashSet<int>();
			public bool AllAnimations;
			public bool AllLayers;
			public bool LoopFrames;
			public bool AddEmptyFrame;
			public int AnimLength;
			public int AnimStart;

			public AnimationData() {
				Animations.Add(0);
				Layers.Add(0);
			}

			public void SetAnimation(params int[] aids) {
				Animations.Clear();

				foreach (var aid in aids)
					Animations.Add(aid);
			}

			public void SetLayers(int layerIndex) {
				Layers.Clear();
				Layers.Add(layerIndex);
			}

			public AnimationData Copy() {
				AnimationData saveData = new AnimationData();
				Animations = new HashSet<int>(saveData.Animations);
				Layers = new HashSet<int>(saveData.Layers);
				AllAnimations = saveData.AllAnimations;
				AllLayers = saveData.AllLayers;
				LoopFrames = saveData.LoopFrames;
				AddEmptyFrame = saveData.AddEmptyFrame;
				AnimLength = saveData.AnimLength;
				AnimStart = saveData.AnimStart;

				return saveData;
			}

			public string ExportSetting() {
				List<string> data = new List<string>();
				StringBuilder b = new StringBuilder();

				foreach (var animation in Animations) {
					b.Append(animation);
					b.Append(";");
				}

				data.Add(b.ToString());

				b.Clear();

				foreach (var layer in Layers) {
					b.Append(layer);
					b.Append(";");
				}

				data.Add(b.ToString());
				data.Add(AllAnimations.ToString());
				data.Add(AllLayers.ToString());
				data.Add(LoopFrames.ToString());
				data.Add(AddEmptyFrame.ToString());
				data.Add(AnimLength.ToString());
				data.Add(AnimStart.ToString());

				return Methods.ListToString(data, "|");
			}

			public void ImportSetting(string settings) {
				var data = Methods.StringToList(settings, '|');
				
				Animations.Clear();

				foreach (var animation in data[0].Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
					Animations.Add(Int32.Parse(animation));

				Layers.Clear();

				foreach (var layer in data[1].Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
					Layers.Add(Int32.Parse(layer));

				AllAnimations = Boolean.Parse(data[2]);
				AllLayers = Boolean.Parse(data[3]);
				LoopFrames = Boolean.Parse(data[4]);
				AddEmptyFrame = Boolean.Parse(data[5]);
				AnimLength = Int32.Parse(data[6]);
				AnimStart = Int32.Parse(data[7]);
			}
		}

		public AnimationData SaveData = new AnimationData();
		public AnimationData DefaultSaveData = new AnimationData();

		public AnimationEditComponent(Act act) : this(act, AnimationEditTypes.Full) {
		}

		public AnimationEditComponent(Act act, AnimationEditTypes enabledTypes) : this() {
			_act = act;

			var animations = act.GetAnimations();
			var style = Application.Current.Resources["DarkToggleButtonStyle"] as Style;

			_tbAnimAll = new ToggleButton();
			_tbAnimAll.Content = "All";
			_tbAnimAll.Click += _tbAnimAll_Click;
			_applyTbStyle(_tbAnimAll, style);
			_wpAnimations.Children.Add(_tbAnimAll);

			for (int i = 0; i < animations.Count; i++) {
				ToggleButton tb = new ToggleButton();
				tb.Content = "" + i;
				tb.Click += (s, e) => _bAnimation_Click(s, e, i);
				tb.ToolTip = animations[i];
				_applyTbStyle(tb, style);
				_toggleAnimButtons.Add(tb);
				_wpAnimations.Children.Add(tb);
			}


			_tbLayerAll = new ToggleButton();
			_tbLayerAll.Content = "All";
			_tbLayerAll.Click += _tbLayerAll_Click;
			_wpLayers.Children.Add(_tbLayerAll);
			_applyTbStyle(_tbLayerAll, style);

			int maxLayers = _act.GetAllFrames().Max(p => p.Layers.Count);

			for (int i = 0; i < maxLayers; i++) {
				ToggleButton tb = new ToggleButton();
				tb.Content = "" + i;
				tb.Click += (s, e) => _bLayer_Click(s, e, i);
				_applyTbStyle(tb, style);
				_toggleLayerButtons.Add(tb);
				_wpLayers.Children.Add(tb);
			}

			_tbLength.TextNoEvent = "0";
			_tbStart.TextNoEvent = "0";

			_tbLength.MinValue = 0;
			_tbLength.MaxValue = 100;
			_tbLength.DeltaMultiplier = 0.5f;

			_tbStart.MinValue = 0;
			_tbStart.MaxValue = 100;
			_tbStart.DeltaMultiplier = 0.5f;

			_tbLength.TextChanged += _tbLength_TextChanged;
			_tbStart.TextChanged += _tbStart_TextChanged;

			_tbLength.Init(p => {
				if (Int32.TryParse(p, out int val) && val == 0) {
					_tbLength._previewMid.Text = "All";
				}
				else {
					_tbLength._previewMid.Text = p;
				}
			});

			WpfUtilities.AddMouseInOutUnderline(_cbLoop);
			WpfUtilities.AddMouseInOutUnderline(_cbEmptyFrame);

			_cbLoop.Checked += _cb_Checked;
			_cbEmptyFrame.Checked += _cb_Checked;

			_cbLoop.Unchecked += _cb_Checked;
			_cbEmptyFrame.Unchecked += _cb_Checked;
			SetEditType(enabledTypes);
		}

		public void SetEditType(AnimationEditTypes enabledTypes) {
			if (!enabledTypes.HasFlag(AnimationEditTypes.Animation))
				_gridAnimation.Visibility = Visibility.Collapsed;
			if (!enabledTypes.HasFlag(AnimationEditTypes.Layer))
				_gridLayers.Visibility = Visibility.Collapsed;
			if (!enabledTypes.HasFlag(AnimationEditTypes.Loop))
				_cbLoop.Visibility = Visibility.Collapsed;
			if (!enabledTypes.HasFlag(AnimationEditTypes.AddEmptyFrame))
				_cbEmptyFrame.Visibility = Visibility.Collapsed;
			if (!enabledTypes.HasFlag(AnimationEditTypes.Length)) {
				_tbLength.Visibility = Visibility.Collapsed;
				_tblockLength.Visibility = Visibility.Collapsed;
			}
			if (!enabledTypes.HasFlag(AnimationEditTypes.Start)) {
				_tbStart.Visibility = Visibility.Collapsed;
				_tblockStart.Visibility = Visibility.Collapsed;
			}
		}

		private void _applyTbStyle(ToggleButton tb, Style style) {
			if (style != null)
				tb.Style = style;

			tb.Width = 30;
			tb.Height = 20;
			tb.Padding = new Thickness(0);
			tb.VerticalContentAlignment = VerticalAlignment.Center;
		}

		private void _cb_Checked(object sender, RoutedEventArgs e) {
			if (!_eventsEnabled) return;

			SaveData.LoopFrames = _cbLoop.IsChecked == true;
			SaveData.AddEmptyFrame = _cbEmptyFrame.IsChecked == true;
			SaveAndUpdate();
		}

		private void _tbStart_TextChanged(WPF.InteractionComponent.AdvancedTextBox sender, TextChangedEventArgs e, bool commands) {
			if (!_eventsEnabled) return;

			Int32.TryParse(_tbStart.Text, out SaveData.AnimStart);
			SaveAndUpdate();
		}

		private void _tbLength_TextChanged(WPF.InteractionComponent.AdvancedTextBox sender, TextChangedEventArgs e, bool commands) {
			if (!_eventsEnabled) return;

			Int32.TryParse(_tbLength.Text, out SaveData.AnimLength);
			SaveAndUpdate();
		}

		private void _updateAnimations() {
			HashSet<int> animations = new HashSet<int>();

			if (_tbAnimAll.IsChecked == true) {
				for (int i = 0; i < _toggleAnimButtons.Count; i++) {
					animations.Add(i);
				}
			}
			else {
				for (int i = 0; i < _toggleAnimButtons.Count; i++) {
					if (_toggleAnimButtons[i].IsChecked == true)
						animations.Add(i);
				}
			}

			SaveData.Animations = animations;
		}

		private void _updateLayers() {
			HashSet<int> layers = new HashSet<int>();

			if (_tbLayerAll.IsChecked == true) {
				for (int i = 0; i < _toggleLayerButtons.Count; i++) {
					layers.Add(i);
				}
			}
			else {
				for (int i = 0; i < _toggleLayerButtons.Count; i++) {
					if (_toggleLayerButtons[i].IsChecked == true)
						layers.Add(i);
				}
			}

			SaveData.Layers = layers;
		}

		private void _bAnimation_Click(object sender, RoutedEventArgs e, int aid) {
			if (!_eventsEnabled) return;

			DisableEvents();
			if (_tbAnimAll.IsChecked == true) {
				if (!Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) {
					_toggleAnimButtons.ForEach(p => p.IsChecked = false);

					if (sender is ToggleButton tb)
						tb.IsChecked = true;
				}
			}

			_tbAnimAll.IsChecked = false;
			SaveData.AllAnimations = false;
			_updateAnimations();
			EnableEvents();

			SaveAndUpdate();
		}

		private void _bLayer_Click(object sender, RoutedEventArgs e, int layerIndex) {
			if (!_eventsEnabled) return;

			DisableEvents();
			if (_tbLayerAll.IsChecked == true) {
				if (!Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) {
					_toggleLayerButtons.ForEach(p => p.IsChecked = false);

					if (sender is ToggleButton tb)
						tb.IsChecked = true;
				}
			}

			_tbLayerAll.IsChecked = false;
			SaveData.AllLayers = false;
			_updateLayers();
			EnableEvents();

			SaveAndUpdate();
		}

		private void _tbAnimAll_Click(object sender, RoutedEventArgs e) {
			if (!_eventsEnabled) return;

			DisableEvents();
			if (_tbAnimAll.IsChecked == true) {
				_toggleAnimButtons.ForEach(p => p.IsChecked = true);
				SaveData.AllAnimations = true;
			}
			else if (_tbAnimAll.IsChecked == false) {
				_toggleAnimButtons.ForEach(p => p.IsChecked = false);
				SaveData.AllAnimations = false;
			}
			_updateAnimations();
			EnableEvents();

			SaveAndUpdate();
		}

		private void _tbLayerAll_Click(object sender, RoutedEventArgs e) {
			if (!_eventsEnabled) return;

			DisableEvents();
			if (_tbLayerAll.IsChecked == true) {
				_toggleLayerButtons.ForEach(p => p.IsChecked = true);
				SaveData.AllLayers = true;
			}
			else if (_tbLayerAll.IsChecked == false) {
				_toggleLayerButtons.ForEach(p => p.IsChecked = false);
				SaveData.AllLayers = false;
			}
			_updateLayers();
			EnableEvents();

			SaveAndUpdate();
		}

		public void DisableEvents() {
			_eventsEnabledCounter--;

			if (_eventsEnabledCounter < 0)
				_eventsEnabled = false;
		}

		public void EnableEvents() {
			_eventsEnabledCounter++;

			if (_eventsEnabledCounter >= 0)
				_eventsEnabled = true;
		}

		public void CreateTemplate(EffectPreviewDialog.RowCreationData rowData) {
			_update = rowData.Update;
			this.SetValue(Grid.RowProperty, rowData.CurrentRowIndex);
			this.SetValue(Grid.ColumnSpanProperty, 5);
			rowData.GridProperties.Children.Add(this);

			LoadProperty();
		}

		public void SaveProperty() {
			EffectConfiguration.ConfigAsker[_effectProperty.SettingName] = SaveData.ExportSetting();
		}

		public void SaveAndUpdate() {
			SaveProperty();
			_update();
		}

		public void LoadProperty() {
			var defaultSettings = DefaultSaveData.ExportSetting();

			var data = EffectConfiguration.ConfigAsker[_effectProperty.SettingName];
			if (data == null)
				data = defaultSettings;
			SaveData.ImportSetting(data);

			DisableEvents();
			if (SaveData.AllAnimations) {
				_tbAnimAll.IsChecked = true;
				_toggleAnimButtons.ForEach(p => p.IsChecked = true);
			}
			else {
				_tbAnimAll.IsChecked = false;
				_toggleAnimButtons.ForEach(p => p.IsChecked = false);
				foreach (var index in SaveData.Animations) {
					if (index < _toggleAnimButtons.Count)
						_toggleAnimButtons[index].IsChecked = true;
				}
			}

			if (SaveData.AllLayers) {
				_tbLayerAll.IsChecked = true;
				_toggleLayerButtons.ForEach(p => p.IsChecked = true);
			}
			else {
				_tbLayerAll.IsChecked = false;
				_toggleLayerButtons.ForEach(p => p.IsChecked = false);
				foreach (var index in SaveData.Layers) {
					if (index < _toggleLayerButtons.Count)
						_toggleLayerButtons[index].IsChecked = true;
				}
			}

			_cbLoop.IsChecked = SaveData.LoopFrames;
			_cbEmptyFrame.IsChecked = SaveData.AddEmptyFrame;
			_tbLength.TextNoEvent = SaveData.AnimLength.ToString();
			_tbStart.TextNoEvent = SaveData.AnimStart.ToString();
			_updateAnimations();
			_updateLayers();
			EnableEvents();
		}

		public void SetEffectProperty(EffectConfiguration.EffectProperty effectProperty) {
			_effectProperty = effectProperty;
		}
	}
}
