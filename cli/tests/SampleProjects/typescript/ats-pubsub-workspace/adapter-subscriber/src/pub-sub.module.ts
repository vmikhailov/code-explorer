import { PubSubConfig } from './pubsub.config';

export class PubSubModule {
  static forRootWithAuthFile(options: any) {
    return options;
  }
}

const config = new PubSubConfig();
PubSubModule.forRootWithAuthFile({
  topics: [
    {
      name: config.journalTopicName,
      subscriptions: [config.journalSubscriptionName],
    },
  ],
});
