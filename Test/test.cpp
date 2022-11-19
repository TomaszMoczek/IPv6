#include <stdio.h>
#include <stdlib.h>
#include <unistd.h>
#include <errno.h>
#include <string.h>
#include <netdb.h>
#include <sys/types.h>
#include <netinet/in.h>
#include <sys/socket.h>
#include <arpa/inet.h>

#include <string>
#include <iostream>

#define MAXDATASIZE 100

void *get_in_addr(struct sockaddr *sa)
{
	if (sa->sa_family == AF_INET) {
		return &(((struct sockaddr_in*)sa)->sin_addr);
	}

	return &(((struct sockaddr_in6*)sa)->sin6_addr);
}

int main(int argc, char *argv[])
{
	char buf[MAXDATASIZE];
	char s[INET6_ADDRSTRLEN];
	int rv, sockfd, numbytes;  
	struct addrinfo hints, *servinfo, *p;

	if (argc != 4)
	{
		fprintf(stderr, "usage: protocol | client's hostname | port\n");
		exit(1);
	}

	memset(&hints, 0, sizeof hints);
	hints.ai_family = AF_INET6;

	if (std::string(argv[1]).compare("tcp") == 0)
	{
		hints.ai_socktype = SOCK_STREAM;

		if ((rv = getaddrinfo(argv[2], argv[3], &hints, &servinfo)) != 0)
		{
			fprintf(stderr, "getaddrinfo: %s\n", gai_strerror(rv));
			return 1;
		}

		for (p = servinfo; p != NULL; p = p->ai_next)
		{
			if ((sockfd = socket(p->ai_family, p->ai_socktype, p->ai_protocol)) == -1)
			{
				perror("client: socket");
				continue;
			}
			if (connect(sockfd, p->ai_addr, p->ai_addrlen) == -1)
			{
				close(sockfd);
				perror("client: connect");
				continue;
			}
			break;
		}

		if (p == NULL)
		{
			fprintf(stderr, "client: failed to connect\n");
			return 2;
		}

		inet_ntop(p->ai_family, get_in_addr((struct sockaddr *)p->ai_addr), s, sizeof s);
		printf("Connected with %s at port %s\n\n", s, argv[3]);

		freeaddrinfo(servinfo);

		if ((numbytes = recv(sockfd, buf, MAXDATASIZE-1, 0)) == -1)
		{
			perror("recv");
			exit(1);
		}

		buf[numbytes] = '\0';
		printf("%s\n\n", buf);

		while (true)
		{
			std::string string;
			std::cin >> string;
			if (string.compare("quit") == 0)
			{
				break;
			}
			if ((numbytes = send(sockfd, string.c_str(), string.size(), 0)) == -1)
			{
				perror("send");
				exit(1);
			}
		}

		close(sockfd);
		printf("\nDisconnected with %s\n", s);
	}
	else if (std::string(argv[1]).compare("udp") == 0)
	{
		hints.ai_socktype = SOCK_DGRAM;

		if ((rv = getaddrinfo(argv[2], argv[3], &hints, &servinfo)) != 0) 
		{
			fprintf(stderr, "getaddrinfo: %s\n", gai_strerror(rv));
			return 1;
		}

		for (p = servinfo; p != NULL; p = p->ai_next)
		{
			if ((sockfd = socket(p->ai_family, p->ai_socktype, p->ai_protocol)) == -1)
			{
				perror("talker: socket");
				continue;
			}
			break;
		}

		if (p == NULL) 
		{
			fprintf(stderr, "talker: failed to create socket\n");
			return 2;
		}

		inet_ntop(p->ai_family, get_in_addr((struct sockaddr *)p->ai_addr), s, sizeof s);
		printf("Sending UDPv6 Messages to %s at port %s\n\n", s, argv[3]);

        while (true)
        {
			std::string string;
			std::cin >> string;
			if (string.compare("quit") == 0)
			{
				string.clear();
			}
			if ((numbytes = sendto(sockfd, string.c_str(), string.size(), 0, p->ai_addr, p->ai_addrlen)) == -1)
			{
				perror("talker: sendto");
				exit(1);
			}
            if (string.empty())
            {
                break;
            }
        }

		freeaddrinfo(servinfo);

		close(sockfd);
		printf("\nStopped sending the UDPv6 Messages to %s\n", s);
	}
	else
	{
		std::cout << "Input arguments are not recognizable !" << std::endl;
	}

	return 0;
}
