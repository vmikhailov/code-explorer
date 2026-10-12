import { createClient } from '@clickhouse/client';
import { BigQuery } from '@google-cloud/bigquery';

const clickhouse = createClient({
  url: 'http://localhost:8123',
  database: 'analytics_db'
});

const bigquery = new BigQuery();

export async function runOlapPipelines() {
  // ClickHouse analytics query
  const chEvents = await clickhouse.query({
    query: 'SELECT event_type, count() as total FROM analytics_db.events GROUP BY event_type',
    format: 'JSONEachRow'
  });

  // ClickHouse direct insert
  await clickhouse.insert({
    table: 'aggregated_metrics',
    values: [{ metric_name: 'daily_active_users', value: 1250 }]
  });

  // BigQuery warehouse query
  const [bqRows] = await bigquery.query('SELECT user_id, sum(revenue) as total_rev FROM `enterprise_dw.user_revenue` GROUP BY user_id');

  // BigQuery table insert
  const table = bigquery.dataset('enterprise_dw').table('daily_summaries');
  await table.insert([{ summary_date: '2026-10-12', total_users: 1250 }]);
}
