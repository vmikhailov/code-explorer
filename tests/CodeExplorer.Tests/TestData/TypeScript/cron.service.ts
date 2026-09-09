import path from 'path';
import BQ from '../bq.driver';
import { createSqlFileLabels, readBigQuerySqlFile } from '../bq.utility';
import { DAILY_CRON_RUN_HOUR_UTC } from '../constants';
import { getDelayUntilNextUtcHour } from '../helpers';
import Logger from '../logger';

class BundleInvalidsCleanupCron {
  private readonly tag = 'BundleInvalidsCleanupCron';

  start(): void {
    const scheduleNextRun = () => {
      const delay = getDelayUntilNextUtcHour(DAILY_CRON_RUN_HOUR_UTC);
      const nextRunAt = new Date(Date.now() + delay).toISOString();

      new Logger({ nextRunAt }).setTag(this.tag).setDescription('Bundle invalids cleanup cron scheduled').log();

      setTimeout(async () => {
        try {
          new Logger('Bundle invalids cleanup cron started').setTag(this.tag).log();

          const queryPath = path.resolve('src/sql/delete-bundle-invalids.sql');
          const query = readBigQuerySqlFile(queryPath);
          const labels = createSqlFileLabels(queryPath);

          await BQ.executeQuery(query, labels);

          new Logger('Bundle invalids cleanup cron finished').setTag(this.tag).log();
        } catch (e) {
          new Logger(e).setTag(this.tag).error();
        } finally {
          scheduleNextRun();
        }
      }, delay);
    };

    scheduleNextRun();
  }
}

export const bundleInvalidsCleanupCron = new BundleInvalidsCleanupCron();
