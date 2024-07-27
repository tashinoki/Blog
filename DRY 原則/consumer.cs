
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

enum ConsumerRank
{
	Bronze,
	Silver,
	Gold,
	Platinum
}
