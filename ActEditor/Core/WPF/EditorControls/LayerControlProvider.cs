using System;
using System.Collections.Generic;
using System.Windows.Threading;
using ActEditor.Core.WPF.Dialogs;
using TokeiLibrary;

namespace ActEditor.Core.WPF.EditorControls {
	/// <summary>
	/// Provides LayerControl objets by buffering them in the background.
	/// This speeds up the rendering speed.
	/// </summary>
	public class LayerControlProvider {
		public const int MinimumAmountToGenerateAndLoad = 20;
		public const int MinimumAmountToGenerate = 100;
		private static readonly Action _emptyDelegate = delegate { };

		private readonly TabAct _actEditor;
		private readonly Dictionary<int, LayerControl> _controls = new Dictionary<int, LayerControl>();

		public LayerControlProvider(TabAct actEditor) {
			_actEditor = actEditor;

			for (int i = 0; i < MinimumAmountToGenerateAndLoad; i++) {
				var ctr = new LayerControl(null, actEditor, i);
				actEditor._preloader.Children.Add(ctr);
				ctr.Dispatcher.Invoke(DispatcherPriority.Render, _emptyDelegate);
				_controls[i] = ctr;
			}

			for (int i = MinimumAmountToGenerateAndLoad; i < MinimumAmountToGenerate; i++) {
				_controls[i] = new LayerControl(null, actEditor, i);
			}
		}

		public LayerControl Get(int index) {
			if (!_controls.ContainsKey(index))
				_controls[index] = new LayerControl(null, _actEditor, index);

			var ctr = _controls[index];

			if (ctr.Parent == _actEditor._preloader) {
				_actEditor.Dispatch(p => p._preloader.Children.Remove(ctr));
			}

			return ctr;
		}
	}
}