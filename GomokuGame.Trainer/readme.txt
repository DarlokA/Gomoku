GomokuGame Trainer

Режим 1. train algorithm:standard (по умолчанию)
			board — размер доски (по умолчанию 9)
			win — длина выигрышной линии (по умолчанию 5)
			games — число партий, 0 = бесконечно (по умолчанию 10000)
			lr — learning rate (по умолчанию 0.001)
			epsilon — стартовый epsilon для новой сети (по умолчанию 0.3; при продолжении обучения берётся 0.1)
			decay — скорость затухания epsilon (по умолчанию 0.9995)
			mode — режим обучения: A/B/C/D (по умолчанию C)
			correction — режим коррекции критического буфера: Full/Once/Off/All (по умолчанию Full)
			chunk — размер чанка партий между логами (по умолчанию 500)
			save — имя файла сети без .json (по умолчанию network)
			demo — показывать ли демо-партию после чанка, 1/0 (по умолчанию 1)

		Пример использования:
		GomokuGame.Trainer.exe train algorithm:standard board:9 win:5 games:20000 save:network_a

Режим 2. train algorithm:league (Игра с замороженным противником. Сеть по состоянию на конец предыдущего чанка)
			board — размер доски (по умолчанию 9)
			win — длина выигрышной линии (по умолчанию 5)
			games — число партий, 0 = бесконечно (по умолчанию 10000)
			lr — learning rate (по умолчанию 0.001)
			epsilon — стартовый epsilon для новой сети (по умолчанию 0.3; при продолжении обучения берётся 0.1)
			decay — скорость затухания epsilon (по умолчанию 0.9995)
			mode — режим обучения: A/B/C/D (по умолчанию C)
			correction — режим коррекции критического буфера: Full/Once/Off/All (по умолчанию Full)
			chunk — размер чанка партий между логами (по умолчанию 500)
			save — имя файла сети без .json (по умолчанию network)
			demo — показывать ли демо-партию после чанка, 1/0 (по умолчанию 1)
			warmup — число партий прогрева перед включением лиги снапшотов (по умолчанию 500)
			snapshotEvery — как часто брать новый снапшот соперника (по умолчанию 100)

		Пример использования:		
		GomokuGame.Trainer.exe train algorithm:league board:9 win:5 games:50000 warmup:1000 snapshotEvery:200 save:network_league

Режим 3. train algorithm:genetic 
			board — размер доски (по умолчанию 9)
			win — длина выигрышной линии (по умолчанию 5)
			chunk — число игр обучения на особь за цикл (по умолчанию 500)
			mode — режим обучения: A/B/C/D (по умолчанию C)
			population — размер популяции (по умолчанию = число ядер процессора)
			eliteCount — сколько особей-элиты переживает цикл без мутации (по умолчанию population/2)
			mutationSigma — сила мутации весов (по умолчанию 0.05)
			gamesPerPair — число партий в каждом турнирном матче пары (по умолчанию 100)
			cycles — число полных циклов эволюции, 0 = бесконечно, выход по Ctrl+C (по умолчанию 0)
			save — имя файла (по умолчанию network)

		Пример использования (короткий тестовый прогон, 3 цикла):
		GomokuGame.Trainer.exe train algorithm:genetic board:9 win:5 chunk:50 population:4 eliteCount:2 mutationSigma:0.05 gamesPerPair:20 cycles:3 save:test_sigma

		Пример использования (длинный прогон без ограничения циклов):
		GomokuGame.Trainer.exe train algorithm:genetic board:9 win:5 population:8 eliteCount:4 mutationSigma:0.03 gamesPerPair:100 save:network_genetic

Режим 4. train algorithm:bot партии тренируются против бота вместо самостоятельной игры сети.
			board — размер доски (по умолчанию 9)
			win — длина выигрышной линии (по умолчанию 5)
			games — число партий, 0 = бесконечно (по умолчанию 10000)
			lr — learning rate (по умолчанию 0.001)
			epsilon — стартовый epsilon для новой сети (по умолчанию 0.3; при продолжении обучения берётся 0.1)
			decay — скорость затухания epsilon (по умолчанию 0.9995)
			mode — режим обучения: A/B/C/D (по умолчанию C)
			correction — режим коррекции критического буфера: Full/Once/Off/All (по умолчанию Full)
			chunk — размер чанка партий между логами (по умолчанию 500)
			save — имя файла сети без .json (по умолчанию network)
			demo — показывать ли демо-партию после чанка, 1/0 (по умолчанию 1)
			
		Пример использования:
		GomokuGame.Trainer.exe train algorithm:bot board:9 win:5 games:20000 save:network_bot

Режим 5. match тестовая игра между сетями
			board — размер доски (по умолчанию 9)
			win — длина выигрышной линии (по умолчанию 5)
			games — число партий матча (по умолчанию 1000)
			x — имя сети/файла для X, либо random (по умолчанию network)
			o — имя сети/файла для O, либо random (по умолчанию network)

		Пример использования:
		GomokuGame.Trainer.exe match board:9 win:5 games:500 x:network_a o:network_b

Режим 6. matchlog тестовая игра между сетями с логированием партий
			board — размер доски (по умолчанию 15)
			win — длина выигрышной линии (по умолчанию 5)
			x, o — имена сетей, либо random (по умолчанию network)
			epsilon — epsilon для обеих сетей в этой одной партии (по умолчанию 0.0)
			log — файл для записи лога партии; без ключа лог идёт в консоль

		Пример использования:
		GomokuGame.Trainer.exe matchlog board:9 win:5 x:network_a o:network_b epsilon:0 log:match1.txt

