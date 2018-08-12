# player-loader
Service to load specific players before a race. 


## TODO 

### Hvordan kjøre opp race-track og playres lokalt

Lag et subnet

        docker network create --subnet=172.18.0.0/16 slotcarai-subnet

STart race-track

        docker run --net slotcarai-network  --ip 172.18.0.2 -it race-track-image

Start spillere

        docker run --net slotcarai-network  -it player-image