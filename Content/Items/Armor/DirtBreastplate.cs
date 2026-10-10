using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace RandAwisor.Content.Items.Armor
{
	[AutoloadEquip(EquipType.Body)]
	public class DirtBreastplate : ModItem
	{
		public override void SetDefaults() {
			Item.width = 30;
			Item.height = 20;
			Item.value = Item.sellPrice(copper: 50);
			Item.rare = ItemRarityID.White;
			Item.defense = 2;
		}

		public override void AddRecipes() {
			CreateRecipe()
				.AddIngredient(ItemID.DirtBlock, 25)
				.AddTile(TileID.WorkBenches)
				.Register();
		}
	}
}
