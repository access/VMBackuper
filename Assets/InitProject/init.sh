#/bin/sh

# init needed dirs
mkdir -p /var/www
mkdir -p /var/www/vmbackuper

# copy files
echo "--------------------------------"
echo -n "Copy needed configuration files... "
\cp -fR html /var/www/vmbackuper
\cp -fR apache2.conf /var/www/vmbackuper
echo "OK"
echo "--------------------------------"

# init services for start on reboot
echo "--------------------------------"
echo -n "Init system for startup services... "
systemctl enable docker.service
systemctl enable containerd.service
echo "OK"
echo "--------------------------------"

# ensure for existings and delete previous
echo "--------------------------------"
echo "Remove previous docker images... "
docker stop vmbackuper-frontend 2>/dev/null
docker stop vmbackuper-backend 2>/dev/null
docker stop apache2-container 2>/dev/null

docker rm vmbackuper-frontend 2>/dev/null
docker rm vmbackuper-backend 2>/dev/null
docker rm apache2-container 2>/dev/null
echo "OK"
echo "--------------------------------"

# init containers, pull it
echo "--------------------------------"
echo "Pull needed docker images... "
docker pull jevgenik/vmbackuper-frontend:latest
docker pull jevgenik/vmbackuper-backend:latest
docker pull ubuntu/apache2:2.4-20.04_beta
echo "OK"
echo "--------------------------------"

# run containers
echo "--------------------------------"
echo "Run docker containers... "
echo "apache2:8080; frontend:5000; backend:7000"
docker run -d --restart=always --name vmbackuper-frontend -p 5000:8000 jevgenik/vmbackuper-frontend
docker run -d --restart=always --name vmbackuper-backend -p 7000:80 jevgenik/vmbackuper-backend
docker run -d --restart=always --name apache2-container -e TZ=UTC -v /var/www/vmbackuper/apache2.conf:/etc/apache2/apache2.conf -v /var/www/vmbackuper/html:/var/www/html -p 8080:80 ubuntu/apache2:2.4-20.04_beta
echo "OK"
echo "--------------------------------"

# show the containers
docker ps -a