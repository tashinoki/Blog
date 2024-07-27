
# DRY ってなんだったっけ
DRY (Don't Repeat Yourself) 原則は「[達人プログラマー：熟達に向けたあなたの旅](https://www.amazon.co.jp/dp/4274226298)」にて初めて登場したプログラミング原則のようですね。この本では以下のように記載されています。

> すべての知識はシステム内において、単一、かつ明確な、そして信頼出来る表現になっていなければならない。

共通化し、システムの中で単一であるべきなのは「**知識**」であって「**コード**」ではないという事が大事なポイントだと思います。

そもそも「**知識**」とはいったいなんやねんという話なんですが、これは「ソフトウェアが課題を解決するために必要な知識」だと考えています。

具体例で考えていきましょう。EC サイトを運営しているとします。以下このサイトの仕様です。

- 購入者は商品を買うとユーザランクに応じてポイントを獲得できる
- ユーザランクはブロンズ、シルバー、ゴールド、プラチナ、の 4 段階ある
- ユーザランク毎の獲得できるポイントの倍率は異なる
- ポイントの計算は税込み前の価格に対して行われる

ポイントを還元させて、ユーザの購買意欲を高める効果を狙っているのでしょうかねー。そうすると、「商品購入数が下がった」といった課題があり、「ユーザの購買意欲の向上」を目指してこういう仕様を考えたのかもしれません。

ソフトウェアでこの仕様を果たすため (=課題を解決するするため) に必要な知識が「ポイント還元率」という事になりますね。まぁ、例としてこういうものを考えてみます。

さて、実際に購入者が商品を購入したときに得られるポイントを計算するには具体的に以下の知識が必要になるはずです。

1. 購入者のユーザランクは何か
2. ユーザランク毎のポイント還元率は何%か

じゃ、ユーザのランクをプラチナとして、ポイント還元率は10%としましょう。商品を買ってポイントと支払金額を計算するプログラムは以下のようになりそうです。

```cs
void Main()
{
	
	var goods = new Goods(1000);
	
	var consumer = new Consumer(ConsumerRank.Platinum);
	
	// 商品購入処理
	var tax = goods.Price * 0.1;
	var point = goods.Price * 0.1;
	
	var payment = goods.Price + tax;
}


class Consumer
{
	private ConsumerRank Rank { get; }
	
	public Consumer(ConsumerRank rank)
	{
		Rank = rank;
	}
}

enum ConsumerRank
{
	Bronze,
	Silver,
	Gold,
	Platinum
}

class Goods
{
	public int Price { get; }
	public Goods(int price)
	{
		Price = price;
	}
}
```

はい、適当ですかこんな感じですかね。夫、ここで注目してほしいのですが、「goods.Price * 0.1」というコードが二箇所に出てきていますね。重複しているし、同じことやってるんだからまとめてしまおう！

```cs
private int Calc(Goods goods)
{
	return (int)Math.Floor(goods.Price * 0.1);
}
```

...はい。これは DRY を誤解しています。なぜか。それは

- ユーザランク毎のポイント還元率
- 消費税

と本質的に異なる知識を同一なものとしてコード上で表現してしまっているからです。見かけ上は 10% で同じように見えるかもしれませんが、値が同じでも意味が異なります。

DRY を誤って使ってしまうとどうなるでしょうか？例えば「プラチナランクのポイント還元率を15%にする」という仕様変更があったとします。太っ腹ですね。

顧客的には嬉しいかもしれませんが、開発者的にはどうでしょうか？`Calc`メソッドを使って計算していた部分を検索し、一つずつ修正していかないといけませんね。漏れてしまえばポイント還元率が異なりビジネス的に大ダメージを受けてしまうかもしれません。少なからずユーザ体験は悪いでしょうね。

じゃ、DRY 原則を守れているとどうなるんでしょうね？例えばこんなコードを書いておきましょう。

```cs
void Main()
{
	
	var goods = new Goods(1000);
	
	var consumer = new Consumer(ConsumerRank.Platinum);
	
	// 商品購入処理
	var totalPrice = Casher.CalcTotalPrice(goods);
	var point = goods.Price * consumer.PoinReturRate;
}


class Consumer
{
	private ConsumerRank Rank { get; }
	
	public Consumer(ConsumerRank rank)
	{
		Rank = rank;
	}

	public double PoinReturRate => Rank　switch
	{
		ConsumerRank.Bronze => 0.01,
		ConsumerRank.Silver => 0.03,
		ConsumerRank.Gold => 0.05,
		ConsumerRank.Platinum => 0.1,
		_ => throw new ArgumentOutOfRangeException(nameof(Rank), "")
	};
}

static class Casher
{
	private static readonly double TaxRate = 0.1;
	
    public static int CalcTotalPrice(Goods goods) => (int)Math.Floor(goods.Price * TaxRate);
	
	public static int CalcTotalPrice(Goods[] goods) => (int)Math.Floor(goods.Sum(g => g.Price) * TaxRate);
}

enum ConsumerRank
{
	Bronze,
	Silver,
	Gold,
	Platinum
}

class Goods
{
	public int Price { get; }
	public Goods(int price)
	{
		Price = price;
	}
}
```

これだとランク毎のポイント還元率と消費税が分離されていて、先ほどの仕様変更も楽に行えそうです。あっちこっちにポイント還元率が登場しているわけでもなく、プラチナに対応する還元率を変えれば他の部分はいじる必要もないですね。本当かな？


いやーこれは例が悪かったかもしれません。とにかく、

- DRY は知識を繰り返し記述することを避けようね
- 「見かけのコードが同じ」だからと言って「知識が同じ」というわけではないよ

と言っていることが伝われば幸いです。