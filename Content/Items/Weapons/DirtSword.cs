using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace RandAwisor.Content.Items.Weapons
{
	public class DirtSword : ModItem
	{
		public override void SetDefaults() {
			Item.width = 32;
			Item.height = 32;

			Item.useStyle = ItemUseStyleID.Swing;
			Item.useTime = 22;
			Item.useAnimation = 22;
			Item.autoReuse = false;

			Item.DamageType = DamageClass.Melee;
			Item.damage = 8;
			Item.knockBack = 5;

			Item.value = Item.sellPrice(copper: 20);
			Item.rare = ItemRarityID.White;
			Item.UseSound = SoundID.Item1;
		}

		public override void AddRecipes() {
			CreateRecipe()
				.AddIngredient(ItemID.DirtBlock, 5)
				.Register();
		}
	}
}
