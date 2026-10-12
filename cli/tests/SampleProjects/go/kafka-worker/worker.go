package main
import "github.com/segmentio/kafka-go"
func main() {
    r := kafka.NewReader(kafka.ReaderConfig{Topic: "events"})
    _ = r
}
