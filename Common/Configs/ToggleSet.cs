using System.Linq;

namespace RandAwisor.Common.Configs
{
	// Base for config subpages made only of bool toggles.
	// tModLoader compares nested config objects with Equals to detect pending changes.
	public abstract class ToggleSet
	{
		public override bool Equals(object obj) {
			return obj?.GetType() == GetType() && GetType().GetFields().All(f => Equals(f.GetValue(this), f.GetValue(obj)));
		}

		public override int GetHashCode() {
			return GetType().GetFields().Aggregate(17, (hash, f) => hash * 31 + f.GetValue(this).GetHashCode());
		}
	}
}
