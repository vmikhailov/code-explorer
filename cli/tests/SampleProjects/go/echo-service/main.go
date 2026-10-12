package main

import (
    "net/http"
    "github.com/labstack/echo/v4"
)

func getRoot(c echo.Context) error {
    return c.String(http.StatusOK, "Echo Service")
}

func listItems(c echo.Context) error {
    return c.JSON(http.StatusOK, []string{"item1", "item2"})
}

func main() {
    e := echo.New()
    e.GET("/", getRoot)

    v1 := e.Group("/api/v1")
    v1.GET("/items", listItems)

    e.Logger.Fatal(e.Start(":1323"))
}
