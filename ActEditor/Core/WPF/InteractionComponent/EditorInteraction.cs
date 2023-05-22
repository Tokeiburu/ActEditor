namespace ActEditor.Core.WPF.InteractionComponent {
	public interface IEditorInteraction {
		/// <summary>
		/// Copy the layers for the associated frame renderer.
		/// </summary>
		void Copy();

		/// <summary>
		/// Paste the layers for the associated frame renderer.
		/// </summary>
		void Paste();

		/// <summary>
		/// Cut the layers for the associated frame renderer.
		/// </summary>
		void Cut();

		/// <summary>
		/// Delete the layers for the associated frame renderer.
		/// </summary>
		void Delete();
	}
}
