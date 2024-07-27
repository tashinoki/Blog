static class Casher
{
	private static readonly double TaxRate = 0.1;
	public static int CalcTotalPrice(Goods goods) => (int)Math.Floor(goods.Price * TaxRate);
	
	public static int CalcTotalPrice(Goods[] goods) => (int)Math.Floor(goods.Sum(g => g.Price) * TaxRate);
}