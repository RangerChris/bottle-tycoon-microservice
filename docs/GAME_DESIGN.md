# Game Design Document for Bottle Tycoon Microservice

## Game Overview  
**Title**: Bottle Tycoon  
**Genre**: Simulation / Tycoon  
**Platform**: Web  
**Target Audience**: Casual gamers  

## Real-Time Game Mechanics  
- **Dynamic Market Prices**: Prices of bottles fluctuate based on supply and demand principles.  
- **Resource Gathering**: Players gather resources in real-time; actions can be queued to optimize gameplay.  
- **Manufacturing Process**: Different bottle types require various production times, leading to strategic decisions on what to manufacture.  

## Customer Queue System  
- **Customer Arrival**: Randomly generated customer arrivals to create a vibrant game environment.  
- **Queue Management**: Players can manage customer queues by optimizing service speed and resource allocation.  
- **Customer Satisfaction**: Different customers have preferences; meeting their needs impacts satisfaction and business reputation.  

## Main Menu  
- **Start Game**: Begin a new game session.  
- **Load Game**: Access previously saved game states.  
- **Options**: Adjust settings like sound, notifications, and gameplay preferences.  
- **Help**: Access a guide and tutorial on game mechanics.  

## Time Management  
- **Real-Time Clock**: The game runs in real-time; actions and events occur based on the in-game clock.  
- **Scheduled Events**: Special game events occur at specific times, incentivizing players to engage at varied hours.  
- **Time Bonuses**: Players may earn bonuses for logging in during peak hours or completing actions quickly.

## Complete Game Session Lifecycle  
1. **Game Start**: Players select new game or load an existing session.  
2. **Tutorial**: An optional tutorial introduces basic mechanics and the customer queue system.  
3. **Main Gameplay**: Managing resources, customer satisfaction, and market dynamics.  
4. **Progress Tracking**: Players track their progress through level milestones and achievements.  
5. **End Game Conditions**: Various scenarios can end the game, such as bankruptcy or reaching a set wealth goal.  
6. **Post-Game Overview**: Players receive feedback on their performance, including areas for improvement and suggestions for the next game session.