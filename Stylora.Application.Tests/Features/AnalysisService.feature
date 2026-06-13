Feature: Season analysis service
  As a Stylora user
  I want my photo analysed into a colour season
  So that my profile reflects my personal palette

  Scenario: A fresh analysis maps the AI result
    Given Gemini classifies the photo as season "Nova" sub-season "Borealis" with recommended colour "#112233"
    When a season analysis is requested
    Then the analysis season is "Nova"
    And the analysis palette contains "#112233"

  Scenario: Saving a profile for an invalid user id is rejected
    When the season profile is saved for user "not-a-guid"
    Then the save is rejected as an invalid user

  Scenario: Saving a valid profile upserts the stored analysis
    Given a stored user exists
    When the season profile is saved for that user
    Then the analysis is upserted for that user

  Scenario Outline: Invalid user ids never reach the analysis repository
    When the latest analysis is requested for user "<userId>"
    Then no analysis is returned
    And the analysis repository was never queried

    Examples:
      | userId     |
      | not-a-guid |
      | 12345      |
      | xyz        |
