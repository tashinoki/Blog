class MyConfiguration: DbConfiguration
{
	public MyConfiguration()
	{
		// SQL Server 以外のDatabaseを使っている場合、DefaultExecutionStrategyが既定のStrategyとなる
		// クエリの実行に失敗してもリトライはしない
		// https://learn.microsoft.com/en-us/dotnet/api/system.data.entity.infrastructure.defaultexecutionstrategy?view=entity-framework-6.2.0
		SetExecutionStrategy("", () => new DefaultExecutionStrategy());
	}
}