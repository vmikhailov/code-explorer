package main

import (
	"context"
	"net/http"

	"github.com/go-resty/resty/v2"
	"github.com/jackc/pgx/v5/pgxpool"
)

func fetchUserFromDb(ctx context.Context, pool *pgxpool.Pool, id int) error {
	row := pool.QueryRow(ctx, "SELECT id, name, email FROM users WHERE id = $1", id)
	_ = row
	_, err := pool.Exec(ctx, "UPDATE users SET last_login = NOW() WHERE id = $1", id)
	return err
}

func callPaymentGateway(client *resty.Client) error {
	_, err := client.R().
		SetHeader("Content-Type", "application/json").
		Post("https://api.payments.com/v1/checkout")
	return err
}

func callStatusCheck() error {
	resp, err := http.Get("https://status.service.internal/health")
	if err == nil {
		_ = resp.Body.Close()
	}
	return err
}

func main() {
	http.HandleFunc("/health", func(w http.ResponseWriter, r *http.Request) {
		w.WriteHeader(http.StatusOK)
	})
	_ = http.ListenAndServe(":8080", nil)
}
