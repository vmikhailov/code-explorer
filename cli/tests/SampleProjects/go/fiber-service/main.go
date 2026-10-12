package main

import "github.com/gofiber/fiber/v2"

func getHealth(c *fiber.Ctx) error {
    return c.SendString("ok")
}

func getUser(c *fiber.Ctx) error {
    return c.JSON(fiber.Map{"user": "alice"})
}

func main() {
    app := fiber.New()
    app.Get("/health", getHealth)

    api := app.Group("/api/v1")
    api.Get("/users", getUser)

    app.Listen(":3000")
}
