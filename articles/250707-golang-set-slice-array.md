---
title: "GolangのArray/Slice/Set"
emoji: "🌟"
type: "tech" # tech: 技術記事 / idea: アイデア
topics: ["go"]
published: true
---

自分の学び用のメモです。

## はじめに
Golangには、.NETなどにある`HashSet<T>`のような重複を排除した集合を表す型が標準では存在しません。似たようなことをやりたければmoduleを入れるか自分で実装する必要があります。その過程で調べたことをまとめます。

## Array(配列)とSlice
### Array(配列)
配列は同じ型のデータを複数まとめて格納するためのデータ構造です。要素数は固定で、初期化時以降変える事は出来ません。

初期化の方法はいくつかあります。

```go
//要素数が5のint型配列を初期化
var arr1 [5]int
fmt.Println(arr1) // Output: [0 0 0 0 0]

//要素数が3で、1,2,3という値を格納したint型配列を初期化
arr2 := [3]int{1,2,3}
fmt.Println(arr2) // Output: [1 2 3]

//要素数が5で、特定indexに値を格納したint型配列を初期化
arr3 := [5]int{1,4:10}
fmt.Println(arr3) // Output: [1 0 0 0 10]
```

Arrayは要素数も含めて一つの型であると認識されます。そのためint型配列通しであっても要素数が異なるint型配列として宣言された変数には代入出来ません。逆に言えば要素数が同じであれば再代入が可能です。上記の配列をベースにすると下記のようになります。

```go
var arr1 [5]int
arr2 := [3]int{1, 2, 3}
arr3 := [5]int{1, 4: 10}

arr1 = arr2 // コンパイルエラー
arr1 = arr3 // これは大丈夫

fmt.Println(arr1) // Output: [0 0 0 0 10]
```

要素数を固定することで、Golangはコンパイル時に必要なメモリサイズを知れます。これにより効率的なメモリの配置が可能になります。
この状態で要素数の異なる変数を割り当てるとどうなるでしょうか？`[5]int`の値を`[3]int`の変数に代入すると元々`[3]int`用に割り当てていたメモリ範囲を超えてしまいます。こういった事を防ぐために要素数を型の一部としてコンパイル時にエラーを吐くようにしているのだと理解しています。
要素数も型である、というのはランタイムのこの辺りで表現しているのかもしれません。
https://github.com/golang/go/blob/6c3b5a2798c83d583cb37dba9f39c47300d19f1f/src/internal/abi/type.go#L271-L275

Golangの配列は値型です。そのためある配列を別の変数に代入すると、配列そのもののコピーが発生します。.NETは参照型だったので、へぇ～という感じです。
値型ゆえに要素数の大きな配列を代入しまくるとパフォーマンスが劣化する可能性があるので注意が必要です。メモリも食います。
参考にサンプルを書いてベンチマークを取ってみましたが、私の環境では実行時間に数倍程度には差が出ました。

```go
func arrayCopy() [20000]int {
	var arr1 [20000]int
	arr3 := [20000]int{1, 4: 10}

	arr1 = arr3
	return arr1
}

func refCopy() *[20000]int {
    var arr1 *[20000]int
    arr3 := [20000]int{1, 4: 10}

    arr1 = &arr3
    return arr1
}
```

### Slice
配列の制約(要素数固定など)は安全にプログラムが書ける一方で不都合さもあります。業務のコードを書く際にはあらかじめ要素数が分かりきっている、というドメインの方が少なく感じます。
アプリケーションの実行時に動的に要素数を変えたい場合はSliceを使います。Sliceは.NETの`List<T>`に近い型です。内部には配列を持ち、コレクションを要さするインターフェース(appendなど)を提供してくれます。
実態は配列で、要素数を超すような場合は要素数を増やして新しく配列を生成してます。

sliceはarrayと違って参照型です。型の定義を見るとわかるのですが、
- 配列の先頭のアドレスを表すポインタ
- 配列の長さ
- 容量
の3つのフィールドから構成されています。この3つの情報をスライスヘッダと言ったりします。

https://github.com/golang/go/blob/54c9d776302d53ab1907645cb67fa4a948e1500c/src/runtime/slice.go#L15-L19

スライスヘッダは上記フィールドを持つ構造で、関数に渡すなどする場合はスライスヘッダのみがコピーされます。内部で持っている配列がコピーされるなどはありません。

```go
//要素数、Capacityが0のSliceを初期化
slice1 := make([]int, 0)
fmt.Println(slice1)      // output: []
fmt.Println(cap(slice1)) // output: 0

var slice2 = int[]{}

slice3 := int[]{1,2,3}
```

## GolangのSet
Golangの標準の機能ではSet(何かしらのルールで重複を排除した集合)は存在しません。mapを使って実装するのが一般的ですが、実装方法はいくつかあります。

### golang-setを使う
https://github.com/deckarep/golang-set

### mapのvalueにboolを使う

```go
type Coins map[int]bool

func main() {

	var coins = Coins{
		1:   true,
		5:   true,
		10:  true,
		50:  true,
		100: true,
		500: true,
	}

	fmt.Println(coins)

	if coins[1] {
		fmt.Println("1円玉があります")
	} else {
		fmt.Println("1円玉はありません")
	}

	if coins[2] {
		fmt.Println("2円玉があります")
	} else {
		fmt.Println("2円玉はありません")
	}
}
```

valueをそのまま存在確認に使えるのでコードの見通しは良くなるかもしれません。ただし、bool型の値は1byteを使うので要素の数×1byteの領域を使います。

### mapのvalueにstruct{}を使う
boolと比較し、`struct{}`はメモリを確保したいため要素の数が多くなってくる場合に使うといいかもしれません。

```go
type Coins map[int]struct{}

func main() {

	var coins = Coins{
		1:   {},
		5:   {},
		10:  {},
		50:  {},
		100: {},
		500: {},
	}

	fmt.Println(coins)

	if _, ok := coins[1]; ok {
		fmt.Println("1円玉があります")
	} else {
		fmt.Println("1円玉はありません")
	}

	if _, ok := coins[2]; ok {
		fmt.Println("2円玉があります")
	} else {
		fmt.Println("2円玉はありません")
	}
}
```

### mapのkeyにcomparableを使う
より汎用的にgenericsも使えます。mapのkeyは==で比較可能である、などといった[制約](https://go.dev/ref/spec#Comparison_operators)があるので、それをgenericsで補償している感じです。
```go
type Set[K comparable] map[K]struct{}

func main() {

	var coins = Set[int]{
		1:   {},
		5:   {},
		10:  {},
		50:  {},
		100: {},
		500: {},
	}

	fmt.Println(coins)

	if _, ok := coins[1]; ok {
		fmt.Println("1円玉があります")
	} else {
		fmt.Println("1円玉はありません")
	}

	if _, ok := coins[2]; ok {
		fmt.Println("2円玉があります")
	} else {
		fmt.Println("2円玉はありません")
	}
}
```
ただこの例だと、int型であればどんな値でもkeyにしてしまうので、ファーストクラスコレクションっぽく作った方が業務では使い勝手が良いかもしれません。

```go
type set[K comparable] map[K]struct{}
type Coins struct {
	set[int]
}

func (c *Coins) Add(coin int) {
	//バリデーションとか
	c.set[coin] = struct{}{}
}
```

### 他試したこと
#### Keyにポインタを使ってみる
当たり前だけど、メモリアドレスの比較になっている。構造体の各フィールドが同じ値でも別のkeyで登録されている。
```go
type User struct {
	Name string
}

func main() {
	users := set[*User]{}

	user1 := &User{Name: "hoge"}
	users[user1] = struct{}{}

	user2 := &User{Name: "fuga"}
	users[user2] = struct{}{}

	user3 := user1
	users[user3] = struct{}{}

	user4 := &User{Name: "hoge"}
	users[user4] = struct{}{}

	fmt.Println(users)
}
```

#### 構造体をkeyにしてみる
全フィールドの値で比較を行なっている。
```go
type User struct {
	Name string
	Age  int
}

func main() {
	users := set[User]{}

	user1 := User{Name: "hoge", Age: 20}
	users[user1] = struct{}{}

	user2 := User{Name: "fuga", Age: 25}
	users[user2] = struct{}{}

	user3 := User{Name: "hoge", Age: 30}
	users[user3] = struct{}{}

	user4 := User{Name: "hoge", Age: 20}
	users[user4] = struct{}{}

	fmt.Println(users) //map[{fuga 25}:{} {hoge 20}:{} {hoge 30}:{}]
}
```
重複なしでユーザの集合を扱いたかったら、ユーザを一意に特定できる値をkeyにしないといけないです。