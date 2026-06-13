Feature: User profile service
  As a Stylora user
  I want my colour-season profile derived from my stored analysis
  So that the app can personalise styling recommendations

  Scenario: Profile for a stored user maps the colour analysis
    Given a stored user named "Ana" with season "Autumn" and sub-season "Deep Autumn"
    When the profile is requested
    Then the profile first name is "Ana"
    And the profile undertone is "Warm"
    And the profile contrast is "High"

  Scenario: Invalid user id yields an empty profile
    Given the requesting user id is "not-a-guid"
    When the profile is requested
    Then the profile is empty
    And the user repository was never queried

  Scenario Outline: Undertone is derived from the season
    Given a stored user named "Ana" with season "<season>" and sub-season "<season> Light"
    When the profile is requested
    Then the profile undertone is "<undertone>"

    Examples:
      | season | undertone |
      | Spring | Warm      |
      | Summer | Cool      |
      | Autumn | Warm      |
      | Winter | Cool      |
