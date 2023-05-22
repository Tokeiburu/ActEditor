using System.Windows.Controls;

namespace ActEditor.Core.DrawingComponents {
	/// <summary>
	/// The drawing component class is used to display items
	/// in the FrameRenderer.
	/// </summary>
	public abstract class DrawingComponent : Control {
		#region Delegates

		public delegate void DrawingComponentDelegate(object sender, int index, bool selected);

		#endregion

		private bool _isSelected;
		public virtual bool IsSelectable { get; set; }

		public virtual bool IsSelected {
			get { return _isSelected; }
			set {
				bool raise = _isSelected != value;

				_isSelected = value;

				if (raise)
					OnSelected(-1, _isSelected);
			}
		}

		public event DrawingComponentDelegate Selected;

		public virtual void OnSelected(int index, bool isSelected) {
			DrawingComponentDelegate handler = Selected;
			if (handler != null) handler(this, index, isSelected);
		}

		/// <summary>
		/// Renders the element in the IPreview object.
		/// </summary>
		/// <param name="renderer">The renderer.</param>
		public abstract void Render(IFrameRenderer renderer);

		/// <summary>
		/// Renders only the essential parts without reloading the elements.
		/// </summary>
		/// <param name="renderer">The renderer.</param>
		public abstract void QuickRender(IFrameRenderer renderer);

		/// <summary>
		/// Removes the element from the IPreview object.
		/// </summary>
		/// <param name="renderer">The renderer.</param>
		public abstract void Remove(IFrameRenderer renderer);


		/// <summary>
		/// Selects the element (if the component supports this operation).
		/// </summary>
		public virtual void Select() {
		}
	}
}