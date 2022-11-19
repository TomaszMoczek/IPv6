default: prog

test.o: test.cpp
	g++ -Wall -g -c test.cpp

prog: test.o
	g++ -Wall -g -o test test.o `g++ --print-file-name=libstdc++.a`

clean:
	rm test.o
