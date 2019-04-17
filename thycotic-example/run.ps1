docker build -t thycotic-test .
docker run --rm -it `
  -e "Thycotic__Uri=$($env:Thycotic:Uri)" `
  -e "Thycotic__RuleName=$($env:Thycotic:RuleName)" `
  -e "Thycotic__RuleKey=$($env:Thycotic:RuleKey)" `
  -e "Thycotic__SearchTemplateName=$($env:Thycotic:SearchTemplateName)" `
  -e "Thycotic__SearchText=$($env:Thycotic:SearchText)" `
  -e "Thycotic__Username=${THYCOTIC_USERNAME}" `
  -e "Thycotic__Password=${THYCOTIC_PASSWORD}" `
  -e "Thycotic__GrantType=${THYCOTIC_GRANT_TYPE}" `
  thycotic-test