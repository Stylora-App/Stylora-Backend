Feature: Wardrobe service
  As a Stylora user
  I want uploaded clothing images validated before they join my wardrobe
  So that only real clothing items are stored

  Scenario: An approved image is saved immediately
    Given the clothing validator approves the image
    When a wardrobe item is added
    Then the item is saved to the wardrobe
    And the item validation status is "pass"

  Scenario: A flagged image is not saved without an override
    Given the clothing validator flags the image with a warning
    When a wardrobe item is added
    Then the item is not saved
    And the response offers an override

  Scenario: A flagged image is saved when the user overrides the warning
    Given the clothing validator flags the image with a warning
    When a wardrobe item is added with the warning overridden
    Then the item is saved to the wardrobe
    And the item validation status is "warning"

  Scenario Outline: The requested category is resolved with a safe fallback
    Given the clothing validator approves the image
    When a wardrobe item is added with category "<category>"
    Then the saved item category is "<resolved>"

    Examples:
      | category  | resolved  |
      | outerwear | Outerwear |
      | DRESS     | Dress     |
      | nonsense  | Top       |
