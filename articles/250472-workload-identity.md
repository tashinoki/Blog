---
title: "Workload Identity Federationを使ってGKEからPubSubへメッセージを飛ばす"
emoji: "📌"
type: "tech"
topics: []
published: true
---

## Kubernetes Service Account を作る
Google Cloud のロールを付与する k8s Service Account を作成します。また、Google Cloud リソース (PubSub) を利用sるう Pod にその SA を付与します。

```yaml
apiVersion: v1
kind: ServiceAccount
metadata:
  name: sample-sa
---
apiVersion: apps/v1
kind: Deployment
metadata:
  name: sample-publisher
  labels:
    app: sample-publisher
spec:
  selector:
    matchLabels:
      app: sample-publisher

  template:
    metadata:
      labels:
        app: sample-publisher
    spec:
      serviceAccountName: sample-sa

```

Service 等は割愛していますが、必要に応じてマニフェストを作成してください。これをクラスタに apply すると、Deployment と ServiceAccount が作成されます。

## k8s Service Account に PubSub Publisher Role を付与する
terraform を使い作成済みの k8s Service Account (sample-sa) に対して PubSub へ Publish を行うロールを付与します。

```terraform
resource "google_project_iam_member" "pub_sub_pusher" {
    project = var.project_id
    role    = "roles/pubsub.publisher"
    member = "principal://iam.googleapis.com/projects/${var.project_number}/locations/global/workloadIdentityPools/${var.project_id}.svc.id.goog/subject/ns/{k9s namespace}/sa/sample-sa"
}
```

このコードで、Google Cloud プロジェクト内 (var.project_id, var.project_number で指定) の PubSub に対して Publish のロールを付与することが出来ます。


## Go アプリを動かしてみる
クラスタ上で稼働させるアプリを Go で書いて、PubSub にメッセージを飛ばしてみます。

```golang
func publishMessage(projectID, topicID, msg string) (string, error) {
	ctx := context.Background()
	client, err := pubsub.NewClient(ctx, projectID)
	if err != nil {
		return "", fmt.Errorf("pubsub.NewClient 作成に失敗しました: %w", err)
	}
	defer client.Close()

	t := client.Topic(topicID)

	result := t.Publish(ctx, &pubsub.Message{
		Data: []byte(msg),
		Attributes: map[string]string{
			"origin":   "csv-parser",
			"username": "hogehoge",
		},
	})

	id, err := result.Get(ctx)
	if err != nil {
		return "", fmt.Errorf("publish結果の取得に失敗しました (Get): %w", err)
	}

	return id, nil
}

func main() {
	projectID := os.Getenv("GCP_PROJECT_ID")
	topicID := os.Getenv("GCP_PUBSUB_TOPIC_ID")

	if projectID == "" {
		log.Fatal("エラー: Google Cloud プロジェクトIDを 'your-gcp-project-id' から実際の値に設定してください。")
	}

	log.Printf("プロジェクト '%s' のトピック '%s' にメッセージを送信します...", projectID, topicID)

	http.HandleFunc("/", func(w http.ResponseWriter, r *http.Request) {

		message := fmt.Sprintf("Hello from Go! Sent at %s", time.Now().Format(time.RFC3339))
		msgID, err := publishMessage(projectID, topicID, message)
		if err != nil {
			log.Fatalf("メッセージの送信に失敗しました: %v", err)
		}

		// 成功した場合、公開されたメッセージのIDをログに出力
		log.Printf("メッセージが正常に送信されました。Message ID: %s\n", msgID)
	})

	log.Println("サーバーを起動中... ポート8080で待機中")
	http.ListenAndServe(":8080", nil)
}

```

物凄く単純なコードで、HTTP リクエストを受け付ける度に特定のトピックへメッセージを飛ばします。Google Cloud コンソール上でサブスクリプションにメッセージが溜まっているはずです。

## まとめ
Workload Identity では、GKE 上のアプリケーションから Google Cloud リソースを扱うためにマッピング用の GSA が必要でした。Workload Identity Federation for GKE の仕組みに乗っかれば、GSA が不要になります。上手く使えれば設計がシンプルになるかもしれません。