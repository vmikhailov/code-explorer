import { PubSub, Message } from '@google-cloud/pubsub';
import { PubSubConfig } from './pubsub.config';

export class KVService {
  private readonly pubsub = new PubSub();
  private readonly pubSubConfig = new PubSubConfig();

  async init(): Promise<void> {
    const topic = this.pubsub.topic(this.pubSubConfig.topicName);
    const subscription = topic.subscription(this.pubSubConfig.subName);

    subscription.on('message', (message: Message) => {
      message.ack();
    });
  }
}
