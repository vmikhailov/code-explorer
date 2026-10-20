import { PubSub } from '@google-cloud/pubsub';

export class PublisherConfig {
  readonly tbUpdatedTopicName: string = process.env.EVENT_TB_UPDATED_TOPIC_NAME || 'event-tb-updated-topic';
  readonly eventBusTopicName: string = process.env.EVENT_BUS_TOPIC_NAME || 'event-bus-topic';
  readonly journalTopicName: string = process.env.JOURNAL_TOPIC_NAME || 'journal-topic';
}

export class PubSubService {
  private readonly pubSub = new PubSub();
  private readonly config = new PublisherConfig();

  async publishTbUpdate(data: object): Promise<void> {
    const topic = this.pubSub.topic(this.config.tbUpdatedTopicName);
    await topic.publishMessage({ json: data });
  }

  async publishEventBus(data: object): Promise<void> {
    const topic = this.pubSub.topic(this.config.eventBusTopicName);
    await topic.publishMessage({ json: data });
  }

  async publishJournal(data: object): Promise<void> {
    const topic = this.pubSub.topic(this.config.journalTopicName);
    await topic.publishMessage({ json: data });
  }
}
