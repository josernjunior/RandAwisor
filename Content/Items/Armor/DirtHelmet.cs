using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace RandAwisor.Content.Items.Armor
{
	[AutoloadEquip(EquipType.Head)]
	public class DirtHelmet : ModItem
	{
		public override void SetDefaults() {
			Item.width = 22;
			Item.height = 20;
			Item.value = Item.sellPrice(copper: 30);
			Item.rare = ItemRarityID.White;
			Item.defense = 1;
		}

		public override void AddRecipes() {
			CreateRecipe()
				.AddIngredient(ItemID.DirtBlock, 15)
				.AddTile(TileID.WorkBenches)
				.Register();
		}
	}
}
