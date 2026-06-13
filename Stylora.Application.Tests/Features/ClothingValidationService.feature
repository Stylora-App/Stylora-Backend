Feature: Clothing validation service
  As a Stylora user
  I want uploaded images checked against reference embeddings
  So that non-clothing uploads are flagged before they pollute my wardrobe

  Scenario: A clear clothing image passes validation
    Given the nearest references contain 3 clothing matches at distance 0.1 and 2 non-clothing matches at distance 0.45
    When the image is validated
    Then the validation status is "Pass"
    And the image is considered likely clothing

  Scenario: A non-clothing image is flagged
    Given the nearest references contain 2 clothing matches at distance 0.45 and 3 non-clothing matches at distance 0.1
    When the image is validated
    Then the validation status is "Warning"
    And the image is not considered likely clothing

  Scenario: A starting worker degrades gracefully instead of failing
    Given the embedding worker is still starting up
    When the image is validated
    Then the validation status is "Warning"
    And the validation message mentions "warming up"

  Scenario Outline: The label mix decides the verdict
    Given the nearest references contain <clothing> clothing matches at distance 0.2 and <nonClothing> non-clothing matches at distance 0.2
    When the image is validated
    Then the validation status is "<status>"

    Examples:
      | clothing | nonClothing | status  |
      | 5        | 0           | Pass    |
      | 3        | 2           | Pass    |
      | 2        | 3           | Warning |
      | 0        | 5           | Warning |
