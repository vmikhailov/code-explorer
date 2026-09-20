import { BIDDING_TYPES } from '../constants';
import {
  IDirectionExternalData,
  IFilteredData,
  ICalibrateMinRoiQuery,
  CalibrateMinRoiRepository,
} from '../repository/calibrate-min-roi.repository';

export class CalibrateMinRoiService {
  private dayNumsThursday: number[];
  private dayNumsFriday: number[];
  private clicksPerProfitableDay: number;
  private repo: CalibrateMinRoiRepository;
  private step: number;

  constructor() {
    // Дни недели, которые нужно учитывать при анализе. 1 = воскресенье, 7 = суббота
    this.dayNumsThursday = [3, 4];
    this.dayNumsFriday = [3, 4, 5];
    this.clicksPerProfitableDay = 10000;
    this.repo = new CalibrateMinRoiRepository();
    this.step = 1.2;
  }

  /**
   * Возвращает список связок, которым необходимо сменить Min Roi
   * @returns {ICalibrateMinRoiQuery[]}
   */
  async calibrateMinRoiQuery(): Promise<ICalibrateMinRoiQuery[]> {
    const dates = this.getWeekDates();

    const excludeBundles = await this.repo.getBundlesWithUpdatedMinRoi(dates.start_date);

    const monday = dates.start_date;
    const sunday = dates.end_date;

    const filteredData = await this.repo.getFilteredData(monday, sunday, excludeBundles);

    const weekComparisonData = this.weekComparison(filteredData, dates);
    const nonProfitableDays = this.nonProfitableDays(weekComparisonData, this.dayNumsFriday);
    const bundleIds = nonProfitableDays.map((row) => row.bundle_id);

    const directionsExternalData = await this.repo.getDirectionExternalData(bundleIds);

    const result = this.calibrateMinRoi(nonProfitableDays, directionsExternalData);
    return result;
  }

  /**
   * Новый расчет minROI
   * @returns {INewMinRoi[]}
   */
  async calibrateMinRoiNew(): Promise<INewMinRoi[]> {
    const dates = this.getWeekDates();

    const excludeBundles = await this.repo.getBundlesWithUpdatedMinRoi(dates.start_date);
    const filteredData = await this.repo.getFilteredData(dates.start_date, dates.end_date, excludeBundles);
    const bundlesHashMap = new Map(filteredData.map((row) => [row.bundle_id, BIDDING_TYPES.CPM.name]));

    const summarizedData = this.summatorFilteredDataByBundleId(filteredData);
    const bundlesDemandingMinRoiChange: IFilteredData[] = summarizedData;
    const vectors = await this.calcVector(bundlesDemandingMinRoiChange);

    const bundlesNewMinRoi: INewMinRoi[] = [];

    for (const vector of vectors) {
      const newMinRoi = this.minRoiCalculator({
        ...vector,
        bidding_type: bundlesHashMap.get(vector.bundle_id) || '',
      });
      const bundleData = summarizedData.find((row) => row.bundle_id === vector.bundle_id);

      if (!bundleData) {
        console.error(`Bundle data not found for bundle_id: ${vector.bundle_id}`);
        continue;
      }

      const mappedData: INewMinRoi = {
        bundle_id: vector.bundle_id,
        clicks: bundleData.clicks,
        profit: bundleData.profit,
        min_roi: vector.min_roi,
        new_min_roi: newMinRoi,
      };

      bundlesNewMinRoi.push(mappedData);
    }
    return bundlesNewMinRoi;
  }

  /**
   * Агрегация данных по связкам
   * @param filteredData
   * @returns
   */
  private summatorFilteredDataByBundleId(filteredData: IFilteredData[]): IFilteredData[] {
    const sumClicksAndProfitByBundle = filteredData.reduce((acc: Map<number, IFilteredData>, row) => {
      const currentRow = acc.get(row.bundle_id);
      if (!currentRow) {
        acc.set(row.bundle_id, row);
      } else {
        currentRow.clicks += row.clicks;
        currentRow.profit += row.profit;
      }

      return acc;
    }, new Map());

    const summarizedData = Array.from(sumClicksAndProfitByBundle.values());
    return summarizedData;
  }

  /**
   *  Восстановление minROI для четверга
   * @returns {ICalibrateMinRoiRecoverQuery[]}
   */
  async recoverThursdayMinRoi(): Promise<ICalibrateMinRoiRecoverQuery[]> {
    const dates = this.getWeekDates();
    return await this.recoverMinRoi(this.dayNumsThursday, dates.current_thursday);
  }

  /**
   * Восстановление minROI для пятницы
   * @returns {ICalibrateMinRoiRecoverQuery[]}
   */
  async recoverFridayMinRoi(): Promise<ICalibrateMinRoiRecoverQuery[]> {
    const dates = this.getWeekDates();
    return await this.recoverMinRoi(this.dayNumsFriday, dates.current_friday);
  }

  /**
   *  Общая логика восстановления minROI для четверга и пятницы
   * @param dayNums
   * @param executeDate
   * @returns {ICalibrateMinRoiRecoverQuery[]}
   */
  private async recoverMinRoi(dayNums: number[], executeDate: Date): Promise<ICalibrateMinRoiRecoverQuery[]> {
    const dates = this.getWeekDates();
    const bundlesWithUpdatedMinRoiBeforeMonday = await this.repo.getBundlesWithUpdatedMinRoiBetweenDates(
      dates.start_date,
      dates.end_date
    );

    const bundlesWithUpdatedMinRoiAtMonday = await this.repo.getBundlesWithUpdatedMinRoiBetweenDates(
      this.getMonday(new Date()),
      this.getMonday(new Date())
    );

    const bundlesWIthUpdatedMinRoiAfterMondey = await this.repo.getBundlesWithUpdatedMinRoi(dates.current_tuesday);

    // Исключить связки до понедельника и после него, у которых менялся minRoi
    const excludeBundles = [...bundlesWithUpdatedMinRoiBeforeMonday, ...bundlesWIthUpdatedMinRoiAfterMondey];

    const dataForComparison = await this.repo.getFilteredData(dates.start_date, executeDate, excludeBundles);

    //Работаем с данными связок, у которых менялся minRoi в понедельник
    const dataForComparisonFilteredByBundles = dataForComparison.filter((row) => {
      return bundlesWithUpdatedMinRoiAtMonday.includes(row.bundle_id);
      //TODO: Использовать true, для дебага логики
      // return true;
    });

    if (dataForComparisonFilteredByBundles.length === 0) {
      return [];
    }

    const weekComparisonData = this.weekComparison(dataForComparisonFilteredByBundles, dates);

    const nonProfitableDays = this.nonProfitableDays(weekComparisonData, dayNums);

    const previousMinRoiResponse = await this.repo.getBundlesMinRoiHistory(
      nonProfitableDays.map((row) => row.bundle_id)
    );

    const calibrateMinRoiRecoverDataArray: ICalibrateMinRoiRecoverQuery[] = [];

    for (const row of nonProfitableDays) {
      const historyData = previousMinRoiResponse.find((item) => item.bundle_id === row.bundle_id);
      if (!historyData || historyData?.old_min_roi === null) {
        //Исключаем сзвязки, у которых не найден предыдущий minRoi
        console.error(`Min roi not found for bundle_id: ${row.bundle_id}`);
        continue;
      }

      const mappedData: ICalibrateMinRoiRecoverQuery = {
        bundle_id: row.bundle_id,
        current_profit: row.current_profit,
        prev_profit: row.prev_profit,
        current_clicks: row.current_clicks,
        prev_clicks: row.prev_clicks,
        previous_min_roi: historyData.old_min_roi,
      };

      calibrateMinRoiRecoverDataArray.push(mappedData);
    }

    return calibrateMinRoiRecoverDataArray;
  }

  /**
   * Расчет нового minROI для связок.
   * @param bundleVector
   * @returns {number}
   */
  private minRoiCalculator(bundleVector: ICalcVectorWithBiddingType): number {
    const { min_roi, vector, bidding_type } = bundleVector;
    let newMinRoi = (min_roi + 1) * Math.pow(this.step, vector) - 1;
    if (bidding_type === BIDDING_TYPES.SMART_CPM.name) {
      const lim = BIDDING_TYPES.SMART_CPM.lim;
      newMinRoi = (min_roi + 1) / 1.2 - 1 < lim ? (min_roi + 1) * 1.2 - 1 : newMinRoi;
      return parseFloat(newMinRoi.toFixed(2));
    }

    if (bidding_type === BIDDING_TYPES.CPM.name) {
      const lim = BIDDING_TYPES.CPM.lim;
      newMinRoi = (min_roi + 1) / 1.2 - 1 < lim ? (min_roi + 1) * 1.2 - 1 : newMinRoi;
      return parseFloat(newMinRoi.toFixed(2));
    }
    return parseFloat(newMinRoi.toFixed(2));
  }

  /**
   * Рассчитывает вектор изменения minROI для связок.
   * @param bundleIds
   * @returns
   */
  private async calcVector(bundlesData: IFilteredData[]): Promise<ICalcVector[]> {
    const bundleIds = bundlesData.map((row) => row.bundle_id);
    const bundlesMinRoiHistory = await this.repo.getBundlesMinRoiHistory(bundleIds);
    const bundlesCurrentMinRoi = await this.repo.getBundlesCurrentMinRoi(bundleIds);

    const vectors: ICalcVector[] = [];

    for (const bundleId of bundleIds) {
      const bundleHistory = bundlesMinRoiHistory.find((bundle) => bundle.bundle_id === bundleId);
      const bundleCurrent = bundlesCurrentMinRoi.find((bundle) => bundle.bundle_id === bundleId);

      if (!bundleCurrent) {
        console.error(`Current min roi not found for bundle_id: ${bundleId}`);
        continue;
      }

      const { min_roi } = bundleCurrent;

      if (!bundleHistory || !bundleHistory.old_min_roi) {
        vectors.push({ bundle_id: bundleId, min_roi: min_roi, vector: 1 });
        continue;
      }

      const { old_min_roi } = bundleHistory;

      if (old_min_roi < min_roi) {
        vectors.push({ bundle_id: bundleId, min_roi: min_roi, vector: 1 });
      } else {
        vectors.push({ bundle_id: bundleId, min_roi: min_roi, vector: -1 });
      }
    }

    return vectors;
  }

  /**
   * Возвращает объект с датами начала и конца текущей и предыдущей недели,
   * включая среду, четверг и пятницу.
   */
  private getWeekDates(): IWeekDates {
    const today = new Date();

    // Текущая неделя (понедельник и воскресенье)
    const currentMonday = this.getMonday(today);
    const currentSunday = new Date(currentMonday);
    currentSunday.setDate(currentMonday.getDate() + 6);

    const currentTuesday = new Date(currentMonday);
    currentTuesday.setDate(currentMonday.getDate() + 1); // Вторник

    const currentWednesday = new Date(currentMonday);
    currentWednesday.setDate(currentMonday.getDate() + 2); // Среда

    const currentThursday = new Date(currentMonday);
    currentThursday.setDate(currentMonday.getDate() + 3); // Четверг

    const currentFriday = new Date(currentMonday);
    currentFriday.setDate(currentMonday.getDate() + 4); // Пятница

    // Предыдущая неделя (понедельник и воскресенье)
    const prevMonday = new Date(currentMonday);
    prevMonday.setDate(currentMonday.getDate() - 7);

    const prevSunday = new Date(prevMonday);
    prevSunday.setDate(prevMonday.getDate() + 6);

    const prevWednesday = new Date(prevMonday);
    prevWednesday.setDate(prevMonday.getDate() + 2); // Среда предыдущей недели

    const prevThursday = new Date(prevMonday);
    prevThursday.setDate(prevMonday.getDate() + 3); // Четверг предыдущей недели

    const prevFriday = new Date(prevMonday);
    prevFriday.setDate(prevMonday.getDate() + 4); // Пятница предыдущей недели

    const prevPrevMonday = new Date(prevMonday);
    prevPrevMonday.setDate(prevMonday.getDate() - 7);

    const prevPrevSunday = new Date(prevSunday);
    prevPrevSunday.setDate(prevSunday.getDate() - 7);

    return {
      start_date: prevMonday, // Дата начала текущей недели
      end_date: prevSunday, // Дата конца текущей недели
      prev_start_date: prevPrevMonday, // Дата начала предыдущей недели
      prev_end_date: prevPrevSunday, // Дата конца предыдущей недели
      current_tuesday: currentTuesday, // Вторник текущей недели
      current_wednesday: currentWednesday, // Среда текущей недели
      current_thursday: currentThursday, // Четверг текущей недели
      current_friday: currentFriday, // Пятница текущей недели
    };
  }

  /**
   * Возвращает дату понедельника для указанной даты.
   */
  private getMonday(date: Date): Date {
    const day = date.getDay();
    const diff = day === 0 ? -6 : 1 - day; // Учитываем, что воскресенье (0) смещается на -6 дней
    return new Date(date.setDate(date.getDate() + diff));
  }

  /**
   * Классифицирует данные по неделям (текущая/предыдущая) и добавляет номер дня недели.
   * @param data - данные из метода getFilteredData
   * @param weekDates - объект с датами текущей и предыдущей недели
   */
  private weekComparison(data: IFilteredData[], weekDates: IWeekDates): IWeekComparisonData[] {
    return data.map((row) => {
      // Определяем неделю, к которой относится дата

      const week =
        row.traffic_day >= weekDates.start_date && row.traffic_day <= weekDates.end_date ? 'prev_week' : 'current_week';

      // Определяем номер дня недели (1 = воскресенье, 7 = суббота)
      const day_num = row.traffic_day.getDay() === 0 ? 7 : row.traffic_day.getDay() + 1;

      return {
        ...row,
        week,
        day_num,
      };
    });
  }

  /**
   * Возвращает связки, которые соответствуют условиям для изменения minROI.
   * @param weekComparisonData - данные из метода weekComparison
   */
  private nonProfitableDays(weekComparisonData: IWeekComparisonData[], dayNums: number[]): INonProfitableDays[] {
    // Группируем данные по связкам
    const groupedByBundle = this.groupBy(weekComparisonData, 'bundle_id');

    const rawData: INonProfitableDays[] = [];

    for (const bundleId in groupedByBundle) {
      const bundleData = groupedByBundle[bundleId];

      // Разделяем данные на текущую и предыдущую недели
      const currentWeekData = bundleData.filter((d) => d.week === 'current_week');
      const prevWeekData = bundleData.filter((d) => d.week === 'prev_week');

      // Фильтруем только (вторник, среду для отката в четверг) или (вторник, среду и четверг для отката в пятницу
      const currentFiltered = currentWeekData.filter((d) => dayNums.includes(d.day_num));
      const prevFiltered = prevWeekData.filter((d) => dayNums.includes(d.day_num));

      // Проверяем минимальное количество дней, когда профит текущей недели выше предыдущей
      const daysProfitable = currentFiltered.filter((currentDay) => {
        const prevDay = prevFiltered.find((prevDay) => prevDay.day_num === currentDay.day_num);
        return prevDay && currentDay.profit > prevDay.profit;
      }).length;

      // Считаем количество дней на предыдущей неделе с кликами >= 10 000
      const fullPrevDays = prevFiltered.filter((prevDay) => prevDay.clicks >= this.clicksPerProfitableDay).length;

      // Считаем общий профит за текущую и предыдущую недели
      const currentProfit = currentFiltered.reduce((sum, d) => sum + d.profit, 0);
      const prevProfit = prevFiltered.reduce((sum, d) => sum + d.profit, 0);
      const currentClicks = currentFiltered.reduce((sum, d) => sum + d.clicks, 0);
      const prevClicks = prevFiltered.reduce((sum, d) => sum + d.clicks, 0);

      rawData.push({
        bundle_id: Number(bundleId),
        current_profit: currentProfit,
        prev_profit: prevProfit,
        days_profitable: daysProfitable,
        full_prev_days: fullPrevDays,
        current_clicks: currentClicks,
        prev_clicks: prevClicks,
      });
    }

    const dataWithProfitableDays = rawData.filter((row) => {
      const validationArray = [
        row.days_profitable >= 2,
        row.current_profit > row.prev_profit,
        row.full_prev_days === dayNums.length,
      ];
      return validationArray.every((check) => check);
    });

    const profitableBundles = dataWithProfitableDays.map((row) => row.bundle_id);

    const nonProfitableDays = rawData.filter((row) => profitableBundles.includes(row.bundle_id) === false);
    return nonProfitableDays;
  }

  /**
   * Группирует массив данных по указанному ключу.
   * @param array - массив данных
   * @param key - ключ для группировки
   */
  private groupBy<T>(array: T[], key: keyof T): Record<string, T[]> {
    return array.reduce((result, currentValue) => {
      const groupKey = String(currentValue[key]);
      if (!result[groupKey]) {
        result[groupKey] = [];
      }
      result[groupKey].push(currentValue);
      return result;
    }, {} as Record<string, T[]>);
  }

  private calibrateMinRoi(
    nonProfitableDays: INonProfitableDays[],
    externalData: IDirectionExternalData[]
  ): ICalibrateMinRoiQuery[] {
    return nonProfitableDays.map((day) => {
      const externalInfo = externalData.find((data) => data.bundle_id === day.bundle_id);

      if (!externalInfo) {
        throw new Error(`External data not found for bundle_id: ${day.bundle_id}`);
      }

      const { min_roi, direction, bidding_type } = externalInfo;

      const defaultNewMinRoi = (min_roi + 1) * 1.2 - 1;

      // Логика расчета new_min_roi
      let new_min_roi: number = defaultNewMinRoi;

      const limitMinRoi = (min_roi + 1) / 1.2 - 1;
      if (bidding_type === 'smart_cpm' && direction === 'down' && limitMinRoi >= 0.1) {
        new_min_roi = limitMinRoi;
      }

      if (bidding_type === 'cpm' && direction === 'down' && limitMinRoi >= 0.3) {
        new_min_roi = limitMinRoi;
      }

      // Округление до двух знаков после запятой
      new_min_roi = parseFloat(new_min_roi.toFixed(2));

      return {
        bundle_id: day.bundle_id,
        current_profit: day.current_profit,
        prev_profit: day.prev_profit,
        days_profitable: day.days_profitable,
        min_roi,
        direction,
        bidding_type,
        new_min_roi,
      };
    });
  }
}

export interface IWeekDates {
  start_date: Date;
  end_date: Date;
  prev_start_date: Date;
  prev_end_date: Date;
  current_tuesday: Date;
  current_wednesday: Date;
  current_thursday: Date;
  current_friday: Date;
}

interface IWeekComparisonData {
  bundle_id: number;
  traffic_day: Date;
  profit: number;
  clicks: number;
  week: string;
  day_num: number;
}

export interface INonProfitableDays {
  bundle_id: number;
  current_profit: number;
  prev_profit: number;
  days_profitable: number;
  full_prev_days: number;
  current_clicks: number;
  prev_clicks: number;
}

interface ICalcVector {
  bundle_id: number;
  min_roi: number;
  vector: number;
}
interface ICalcVectorWithBiddingType extends ICalcVector {
  bidding_type: string;
}

export interface INewMinRoi {
  bundle_id: number;
  clicks: number;
  profit: number;
  min_roi: number;
  new_min_roi: number;
}

interface ICalibrateMinRoiRecoverQuery {
  bundle_id: number;
  current_profit: number;
  prev_profit: number;
  current_clicks: number;
  prev_clicks: number;
  previous_min_roi: number;
}
