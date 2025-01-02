---
title: "Log Ingest API を使って Log Analytics のカスタムテーブルへ書き込む"
emoji: "💭"
type: "tech" # tech: 技術記事 / idea: アイデア
topics: ["azure", "loganalytics"]
published: false
---

# Azure Monitor

## Azure Monitor について

Azure Monitor とは、クラウド環境とオンプレミス環境からの監視データを収集し、分析し、それに対応するための包括的な監視ソリューションのことです。収集、分析など、それぞれを行うためにいくつかのコンポーネントにより構成されています。

例えば、ログの蓄積のために Azure Monitor Logs があり、蓄積したログの分析のために Log Analytics があるようなイメージです。

**クラウド、オンプレ問わず、システムを監視・分析するためのいくつかのサービスの総称が Azure Monitor である**、という理解です。　

上述しましたが、

- Azure Monitor Logs
- Log Analytics

は Azure Monitor の一部です。



参考: https://learn.microsoft.com/en-us/azure/azure-monitor/overview



## Azure Monitor Logs について
Azure Monitor Logs とは、Azure Monitor におけるデータストレージの一種です。Azure Monitor には他にもストレージがあります。

そもそも Azure Monitor では以下 4 種類のデータを収集・蓄積が出来ます。

1. Metrics
2.  Logs
3. Traces
4. Changes

可観測性向上のための 3 本柱と言われる「ログ」「メトリクス」「トレース」の 3 つに加え「変更イベント」対象となっています。

それぞれのデータの説明と、保存されるストレージについて表にまとめました。

|データの種類|記録先|説明|
|-|-|-|
|Metrics|Azure Monitor Metrics|Metrics とは、ある時点においてシステムのある側面を説明した数値データのことです。Azure Monitor Metrics は時系列データを分析するために最適化されたデータストアです。|
|Logs|Azure Monitor Logs|Log とはシステムのイベントを記録したものです。タイムスタンプを含んでおり、**Azure Monitor Logs に記録されます**。Azure Monitor Logs には構造化・非構造化形式のログを保存できます。Log Analytics に Azure Monitor Logs を作り、そこに記録したログは Log Analytics で解析できます。(別 Log Analytics の Monitor Log を解析できるかはわかりません)|
|Traces|Azure Monitor Logs|分散トレーシングは異なるサービスに飛んだリクエストのパスを確認出来るようにします。Trace のデータは、Azure Monitor Logs に記録されます。|
|Changes|Azure Resource Graph|システムやアプリケーションの変更イベントのことです。Azure Resource Grapch というストレージに記録されます。|

Azure Monitor のうち、Log Analytics で分析できるのは Azure Monitor Logs に記録されるのはログとトレースのようです。

参考: https://learn.microsoft.com/en-us/azure/azure-monitor/overview#data-platform

ログの種類には

- Analytics
- Basic

の 2 種類があります。

課金がログの取り込みと保持のそれぞれにかかるので、用途に応じてプランを使い分けコストの最適化をしようって事なのだと思います。

それぞれのプランの詳細は参考記事に書いてあります。私の場合は、「ミドルウェアの可観測性を上げ、運用改善のためのインサイトを得たい」という目的があるため、比較的長期間の保持が求められました。そのため Analytics プランを選択しています。

参考: https://learn.microsoft.com/en-us/azure/azure-monitor/logs/basic-logs-configure?tabs=portal-1#compare-the-basic-and-analytics-log-data-plans

## Log Analytics について

Log Analytics とは、Azure Monitor Logs に対してクエリを実行するために使います。クエリの結果はグラフで視覚化出来ます。例えば、ログをあるフィルタールールで絞り込み、その結果を時系列に並べた折れ線グラフで視覚化。実態は、Azure Monitor Logs の機能の一部のようです。

閾値とアラートの設定も出来ます。例えば、VM のメモリ使用率が 90% を越した時にアラートを発砲するという設定が出来ます。私のチームでは、通知先に Slack を利用しています。

Microsoft Sentinel との連携も出来るようです。出来る事が多い...。

Log Analytics 上では、ログやトレースを記録するためのテーブルが作成できます。サービスの作成時に規定で作られるテーブルもありますし、ユーザが任意のテーブルを作ることも出来ます (このテーブルが Azure Monitor Logs のことで、Log Analytics 経由で作るもの段だと思います)。

テーブル単位でデータの保持期間を指定できますし、カラム定義も行えます。カスタムテーブルのサフィックスには「**_CL**」を付けなければいけません。

参考: https://learn.microsoft.com/en-us/azure/azure-monitor/logs/log-analytics-overview


ドキュメントには
> Each workspace has its own data repository and configuration but might combine data from multiple services

という記述があり、別 workspace からクエリすることも可能なんじゃないかと思ってきた。

参考: https://learn.microsoft.com/en-us/azure/azure-monitor/logs/log-analytics-workspace-overview

# Log Analytics のカスタムテーブルへログを書き込む方法
事前知識の整理がようやく終わりました。実際に書き込むのはここから先で行います。

参考記事: https://learn.microsoft.com/en-us/azure/azure-monitor/logs/tutorial-logs-ingestion-portal

## データコレクションについて

まず DCE と DCR の概念をまとめます。チュートリアルはここがはしょられていて苦労した...。

データコレクションというのは、Azure Data platform の各データソースからストアにデータを収集するための手段のこと。従来は各データソースごとにその手段が異なっており、管理が大変だったとのこと。

それに置き換わる手段として、新しく ETL が実装されています。これを採用すると、従来の方法よりも管理が楽でスケールしやすいそうです。データソースごとに収集方法が共通化されるので、使いまわしはしやすいんだと思います。

この新しい方法の中に DCE, DCR という概念があります。

参考: https://learn.microsoft.com/en-us/azure/azure-monitor/essentials/data-collection

### DCE (Data Collection Endpoint)

DCE は Log 送信を行うカスタムのアプリケーションが Logs Ingestion API (後述) で Azure Monitor へデータを送信するために使います。また、Auzre Monitor Agent も DCE を使いログ収集を行っています。

DCE は以下 2 つのことを行うためのエンドポイントを持っています。

1. Data Ingestion Pipeline にデータを取り込むためのエンドポイント
   - Log Analytics 上のテーブルに書き込むため、というイメージで良いです
2. Azure Monitor Agent が DCR を取得するためのエンドポイント

Data Ingestion はリージョンを跨いでも使えます。例えば、東日本リージョンのアプリケーションから、西日本リージョンの Log Analytics に書き込みを行う場合、西日本リージョンの DCE を使えます。

複数の DCE が必要となるのは以下 2 つの状況のようです。

1. 複数リージョンの Log Analytics へ書き込みを行いたい
   - リージョンごとに DCE が必要
2. 異なるリージョンの Azure Monitor Agent から Log Analytics へ書き込みたい
   - Azure Monitor Agent が DCR を取得するためのエンドポイントはリージョンを跨いで使えないようです

同じリージョンに複数の Log Analytics を作った場合、DCE を使いまわせるかはわかりませんが、一 Log Analytics に一 DCE を作っておけばよさそうです。

正式なものとしてリリースされているのは Log Analytics に対する DCE だけのようですね。Metrics を送信するための DCE はまだ preview のようです。

参考: https://learn.microsoft.com/en-us/azure/azure-monitor/essentials/data-collection-endpoint-overview?tabs=portal

### DCR (Data Collection Rule)

DCR はではデータがどのように・どこに取り込まれるのかを定義できます。Azure Monitor とそこにデータを送信するアプリケーションのインターフェースのようなイメージを持っています。複数のアプリケーションで使いまわせたりも出来ます。

イメージとしては、Azure Monitor が受信したデータを DCR  をもとにどのストレージに保存するのかを決めているような感じです。その過程で、不要なデータを除去したり、都合の良い形に変換したりも出来ます。

DCR 単位での認証も出来ます。

参考: https://learn.microsoft.com/en-us/azure/azure-monitor/essentials/data-collection#data-collection-rules



## 書き込むための API
API を使ってカスタムテーブルに書き込む方法には以下の 2 種類の方法があります。

1. Azure Monitor Log Ingest API
2. Log Analytics HTTP Data Collector API

元々は Data Collector API を使っての方法しかなかったのですが、2022 年 に Log Ingest API というものがリリースされました。現在は Data Collector API は非推奨となっており、**2026 年 9 月 14 日より機能しなくなります**。採用しているプロダクトではそれまでに移行が必要になります。

### Log Ingest API

以下を指定して Log Ingest API を使って Log Analytics のテーブルにデータを送信することが出来ます。

- DCE
- DCR
- DCR の認証情報
- 書き込み先のテーブル名

参考: https://learn.microsoft.com/en-us/azure/azure-monitor/logs/logs-ingestion-api-overview

Data Collector API からの移行ガイドもあるので、まだ実施できていなければぜひ目を通してください。

参考: https://learn.microsoft.com/en-us/azure/azure-monitor/logs/data-collector-api?tabs=powershell

### Data Collection API

非推奨のためリンクだけ貼っておきます。

参考: https://learn.microsoft.com/en-us/azure/azure-monitor/logs/data-collector-api?tabs=powershell


## Azure Function から Log Ingest API を使って書き込みを行う

DCE, DCR, カスタムテーブルは作成済みという前提で進めさせていただきます。また、カスタムテーブルの名前は「User_CL」とし、カラムは以下とします。

|カラム名|型|
|-|-|
|TimeGenerated|datetime|
|Name|string|
|Age|int|
|Information|dynamic|

コードは以下のようなものにしました。

```cs
public class IngestApiSampleFunction
{
    private readonly ILogger<IngestApiSampleFunction> _logger;

    public IngestApiSampleFunction(ILogger<IngestApiSampleFunction> logger)
    {
        _logger = logger;
    }

    [Function(nameof(IngestApiSampleFunction))]
    public async Task Run(
        [TimerTrigger("00:00:30")] TimerInfo timer,
        CancellationToken cancellationToken)
    {
        var logsIngestionClient = new LogsIngestionClient(new Uri(""), new DefaultAzureCredential());
        BinaryData data = BinaryData.FromObjectAsJson(
            new
            {
                TimeGenerated = DateTimeOffset.UtcNow,
                Name = "Computer1",
                Age = 20,
                Information = new
                {
                    Telephone = "000-0000-000",
                    Email = "hoge@sample.com"
                }
            });

        var response = logsIngestionClient.Upload("ruleId", "User_CL", RequestContent.Create(data), context: new RequestContext
        {
            CancellationToken = cancellationToken,
            ErrorOptions = ErrorOptions.Default
        });
    }
}
```

だいぶ簡略化していますが、ポイントは以下です。

1. LogsIngestionClient のインスタンスを作る際に DCE を指定する
2. LogsIngestionClient のインスタンスを作る際に認証情報を渡す
  - 私の場合は Managed Identity に Role を付与し、それを Function に付与しています。そのため、DefaultAzureCredential を渡しています。
3. データ送信時に DCR の ID を指定する
   - DCR はインスタンス生成時に渡した DCE を紐づいている必要があります。
4. データ送信時にテーブル名を指定する
   - 事前に定義していないカラムを送信した場合、自動でカラムが追加されます。
