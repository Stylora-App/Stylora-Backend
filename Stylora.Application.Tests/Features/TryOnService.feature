Feature: Virtual try-on service
  As a Stylora user
  I want to preview a clothing item on my own photo
  So that I can judge the look before buying or wearing it

  Scenario: A successful generation returns the generated image
    Given the AI model generates the image "generated-img"
    When a try-on is generated for person "person-img" and clothing "clothing-img"
    Then the generated image is "generated-img"
    And the session is persisted as successful

  Scenario: A failed generation records the error and surfaces it
    Given the AI model fails with "Gemini unavailable"
    When a try-on is generated for person "person-img" and clothing "clothing-img"
    Then the try-on fails with "Gemini unavailable"
    And the session is persisted as failed with message "Gemini unavailable"

  Scenario Outline: The provided images are forwarded to the AI model unchanged
    Given the AI model generates the image "generated-img"
    When a try-on is generated for person "<person>" and clothing "<clothing>"
    Then the model received person "<person>" and clothing "<clothing>"

    Examples:
      | person   | clothing   |
      | person-a | clothing-a |
      | person-b | clothing-b |
      | person-c | clothing-c |
