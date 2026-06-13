Feature: Outfit chat service
  As a Stylora user
  I want to chat about what to wear
  So that the assistant builds outfits from my own wardrobe

  Scenario: An unrelated conversation is declined
    Given the intent parser finds no outfit request
    And the user says "Tell me a joke."
    When the outfit chat processes the conversation
    Then the chat status is "out_of_scope"

  Scenario: A request without weather context asks a follow-up
    Given the user says "Build me a work outfit."
    When the outfit chat processes the conversation
    Then the chat status is "follow_up"
    And the missing fields include "weather"

  Scenario: An empty wardrobe cannot produce an outfit
    Given any weather resolves to "sunny" at 24 degrees
    And the user says "What should I wear for a casual walk today?"
    When the outfit chat processes the conversation
    Then the chat status is "not_enough_pieces"

  Scenario Outline: A direct request naming a city resolves its weather and builds an outfit
    Given my wardrobe contains a complete casual women wardrobe
    And the city "<city>" has "<status>" weather at <temp> degrees
    And the user says "<message>"
    When the outfit chat processes the conversation
    Then the chat status is "success"
    And the outfit weather summary contains "<status>"

    Examples:
      | city   | status | temp | message                                                         |
      | Brasov | cloudy | 17   | i need an outfit to go out tonight in Brasov for a casual drink |
      | Milan  | sunny  | 24   | What should I wear for a trip to Milan this weekend?            |
