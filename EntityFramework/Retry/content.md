Entity Framework 6 からリトライストラテジーを使って再試行の振る舞いを設定できるようになりました。


こちらの記事を読むと DbConfiguration を使って再試行ストラテジーを仕込む方法が紹介されています。DbConfiguration も EF 6 から使えるようになったみたいですね。

https://github.com/dotnet/EntityFramework.Docs/blob/main/entity-framework/ef6/fundamentals/connection-resiliency/retry-logic.md


# SQL Server でのリトライ

SQL Server と Entity Framework を使う時にこんなコードを書くことがあると思います。

```cs
builder.Services.AddDbContext<MyDbContext>(options =>
{
    options.UseSqlServer("");
});
```

DI コンテナに DbContext を登録する際に、SQL Server の接続文字列を渡してます。`UseSqlServer` メソッドの第二引数には `Action<SqlServerDbContextOptionsBuilder>` を渡せます。なのでこういう風に書くと、割とシンプルに再試行ストラテジーを設定できます。

```cs
builder.Services.AddDbContext<ChangeFeedDbContext>(options =>
{
    options.UseSqlServer("", sqlServerOptions =>
    {
        // 再試行ストラテジーに SqlServerRetryingExecutionStrategy を使う
        // https://learn.microsoft.com/en-us/dotnet/api/microsoft.entityframeworkcore.sqlserverretryingexecutionstrategy?view=efcore-8.0
        sqlServerOptions.EnableRetryOnFailure();
    });
});
```

[SqlServerRetryingExecutionStrategy](https://learn.microsoft.com/en-us/dotnet/api/microsoft.entityframeworkcore.sqlserverretryingexecutionstrategy?view=efcore-8.0)を使って再試行をしてくれるようです。このクラスのコンストラクタを見ると、再試行回数や再試行のディレイを受け取ってくれるみたいですね。これだけ見ると Exponecial Backoff でやってくれるのかしら？と思っちゃいます。

`EnableRetryOnFailure` にはいくつかオーバーロードがあって、再試行回数やディレイの時間を渡せます。これで良しなに再試行ストラテジーを作れます。

`ExecutionStrategy` を使えばカスタムの再試行ストラテジーを使えます。

```cs
builder.Services.AddDbContext<ChangeFeedDbContext>(options =>
{
    options.UseSqlServer("", sqlServerOptions =>
    {
        sqlServerOptions.ExecutionStrategy(d => new CustomRetryStrategy());
    });
});


class CustomRetryStrategy: IExecutionStrategy
{

}

```

もちろんこのコードはビルドエラーが起きます。

# ユーザがトランザクションを明示的に開始したい場合は工夫が必要
例えば以下のようなコードを書いていたとします。※普段はこんなコード書きませんが、よい例が浮かばなかったので。

```cs
async Task RetryWithImplicitTransaction()
{

	var option = new DbContextOptionsBuilder().UseSqlServer("").Options;
	var context = new MyContext(option);

    using var transaction = await context.Database.BeginTransactionAsync();

    var goods = new Goods { Name = "Drink", Price = 100 };
    var order = new Order { Goods = new List<Goods> { goods } };
    
    await context.Orders.AddAsync(order);
    await context.SaveChangesAsync();

    order.Goods.Add(new Goods
    {
        Name = "Food",
        Price = 1000
    });
    
    var orders = await context.Orders.ToArrayAsync();
    
    var totalPrice = orders.Sum(o => o.Goods.Sum(g => g.Price));
    var user = new User { TotalPurchasePrice = totalPrice };
    
    await context.Users.AddAsync(user);
    
    await context.SaveChangesAsync();

    await transaction.CommitAsync();
}
```

`Food` を作成し `TotalPurchasePrice` を更新するタイミングでデータベース操作が失敗しても**再試行は行われません**。

そもそも一貫性を保つ必要があるので、2 度目の SaveChanges だけをやり直すわけにはいきません。2 度目の SaveChangesAsync は 1. `Food`を`Order`に追加、2. `TotalPurchasePrice`の更新、をやっています。この時に 1 は成功して 2 は失敗し、2度目の SaveChangesAsync を丸ごとやり直すとどうなるでしょうか？本来は`Food`を1追加できればよかったのに2つ目が追加され、その結果`TotalPurchasePrice`も増えて整合性が取れなくなってしまいます。

じゃ1度目のSaveChangesAsync からやり直せばよいのですが、そのための情報がありません。なぜなら SaveChangesAsyncを呼び出したタイミングでChangeTrackerがリフレッシュされるからです。

という事で、トランザクションのうちどれか一つでもデータ操作が失敗するとそのトランザクション内の他の操作もロールバックして、もう一度最初からやり直したいわけですが、このままだと出来ません。と理解しています。間違ってたらごめんなさい。

なので、ユーザが明示的にトランザクションを開始する場合には、どの範囲の処理をリトライするのかも明示的にしてあげなければなりません。例えば以下のようにします。

```cs
async Task RetryWithImplicitTransaction()
{
	var option = new DbContextOptionsBuilder().UseSqlServer("").Options;
	var context = new MyContext(option);

	var strategy = context.Database.CreateExecutionStrategy();
	await strategy.Execute(async () =>
	{
		using var transaction = await context.Database.BeginTransactionAsync();

		var goods = new Goods { Name = "Drink", Price = 100 };
		var order = new Order { Goods = new List<Goods> { goods } };
		
		await context.Orders.AddAsync(order);
		await context.SaveChangesAsync();

		order.Goods.Add(new Goods
		{
			Name = "Food",
			Price = 1000
		});
		
		var orders = await context.Orders.ToArrayAsync();
		
		var totalPrice = orders.Sum(o => o.Goods.Sum(g => g.Price));
		var user = new User { TotalPurchasePrice = totalPrice };
		
		await context.Users.AddAsync(user);
		
		await context.SaveChangesAsync();

		await transaction.CommitAsync();
	});
}
```
`strategy` を作り、失敗した場合に再試行をするスコープを明示的にしてあげてください。これをすると2度目のSaveChangesAsyncで失敗した場合は、1度目のSaveChangesAsyncの内容もロールバックして、最初からトランザクションをやり直すという事になるはずですー。