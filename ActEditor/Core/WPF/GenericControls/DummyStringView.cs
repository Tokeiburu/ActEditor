namespace ActEditor.Core.WPF.GenericControls {
	public class DummyStringView {
		private readonly string _item;

		public DummyStringView(string item) {
			_item = item;
		}

		public override string ToString() {
			return _item;
		}
	}
}