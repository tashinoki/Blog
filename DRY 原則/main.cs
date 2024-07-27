void Main()
{
	
	var goods = new Goods(1000);
	
	var consumer = new Consumer(ConsumerRank.Platinum);
	
	// 商品購入処理
	var tax = goods.Price * 0.1;
	var point = goods.Price * 0.1;
	
	var payment = goods.Price + tax;
}
