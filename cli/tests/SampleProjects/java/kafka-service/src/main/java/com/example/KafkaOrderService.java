package com.example;

import org.springframework.kafka.annotation.KafkaListener;
import org.springframework.kafka.core.KafkaTemplate;
import org.springframework.stereotype.Service;

@Service
public class KafkaOrderService {

    private final KafkaTemplate<String, String> kafkaTemplate;

    public KafkaOrderService(KafkaTemplate<String, String> kafkaTemplate) {
        this.kafkaTemplate = kafkaTemplate;
    }

    @KafkaListener(topics = "orders-topic")
    public void processOrder(String message) {
        kafkaTemplate.send("notifications-topic", "Processed: " + message);
    }
}
