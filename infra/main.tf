data "aws_subnet" "default" {
  availability_zone = "eu-north-1a"
  default_for_az    = true
}

module "ec2_instance" {
  source  = "terraform-aws-modules/ec2-instance/aws"
  version = "6.4.1"

  name          = "slowroad-app"
  instance_type = "t3.small"
  key_name      = "web_server_key"
  subnet_id     = data.aws_subnet.default.id

  security_group_ingress_rules = {
    ssh   = { from_port = 22, to_port = 22, cidr_ipv4 = "0.0.0.0/0" }
    http  = { from_port = 80, to_port = 80, cidr_ipv4 = "0.0.0.0/0" }
    https = { from_port = 443, to_port = 443, cidr_ipv4 = "0.0.0.0/0" }
  }

  tags = {
    Project = "slowroad"
    Env     = "dev"
  }
}

output "public_ip" {
  value = module.ec2_instance.public_ip
}