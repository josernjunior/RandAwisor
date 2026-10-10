using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace RandAwisor.Content.Items.Armor
{
	[AutoloadEquip(EquipType.Legs)]
	public class DirtLeggings : ModItem
	{
		public override void SetDefaults() {
			Item.width = 22;
			Item.height = 18;
			Item.value = Item.sellPrice(copper: 36);
			Item.rare = ItemRarityID.White;
			Item.defense = 1;
		}

		public override void AddRecipes() {
			CreateRecipe()
				.AddIngredient(ItemID.DirtBlock, 18)
				.AddTile(TileID.WorkBenches)
				.Register();
		}
	}
}
